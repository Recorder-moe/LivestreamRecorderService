using k8s.Models;
using LivestreamRecorderService.Interfaces.Job;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices.Kubernetes;

public class FC2LiveDLService : KubernetesServiceBase, IFC2LiveDLService
{
    private readonly ILogger<FC2LiveDLService> _logger;

    public override string DownloaderName => "fc2livedl";

    public FC2LiveDLService(
        ILogger<FC2LiveDLService> logger,
        k8s.Kubernetes kubernetes,
        IOptions<KubernetesOption> options,
        IOptions<ServiceOption> serviceOptions,
        IOptions<AzureOption> azureOptions,
        IOptions<NFSOption> nfsOptions) : base(logger, kubernetes, options, serviceOptions, azureOptions, nfsOptions)
    {
        _logger = logger;
    }

    protected override Task<V1Job> CreateNewJobAsync(string _,
                                                     string instanceName,
                                                     string channelId,
                                                     bool useCookiesFile = false,
                                                     CancellationToken cancellation = default)
    {
        try
        {
            return doWithImage("ghcr.io/recorder-moe/fc2-live-dl:2.1.3");
        }
        catch (Exception)
        {
            // Use DockerHub as fallback
            _logger.LogWarning("Failed once, try docker hub as fallback.");
            return doWithImage("recordermoe/fc2-live-dl:2.1.3");
        }

        Task<V1Job> doWithImage(string imageName)
        {
            string[] command = useCookiesFile
                ? new string[]
                {
                    "/usr/bin/dumb-init", "--",
                    "sh", "-c",
                    $"/venv/bin/fc2-live-dl --latency high --threads 1 -o '%(channel_id)s%(date)s%(time)s.%(ext)s' --log-level trace --cookies /fileshare/cookies/{channelId}.txt 'https://live.fc2.com/{channelId}/' && mv *.mp4 /fileshare/"
                }
                : new string[] {
                    "/usr/bin/dumb-init", "--",
                    "sh", "-c",
                    $"/venv/bin/fc2-live-dl --latency high --threads 1 -o '%(channel_id)s%(date)s%(time)s.%(ext)s' --log-level trace 'https://live.fc2.com/{channelId}/' && mv *.mp4 /fileshare/"
                };

            return CreateInstanceAsync(
                    parameters: new
                    {
                        dockerImageName = new
                        {
                            value = imageName
                        },
                        containerName = new
                        {
                            value = instanceName
                        },
                        commandOverrideArray = new
                        {
                            value = command
                        },
                    },
                    deploymentName: instanceName,
                    cancellation: cancellation);
        }
    }
}
