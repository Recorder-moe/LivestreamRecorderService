using k8s.Models;
using LivestreamRecorderService.Interfaces.Job;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices.Kubernetes;

public class TwitcastingRecorderService : KubernetesServiceBase, ITwitcastingRecorderService
{
    private readonly ILogger<TwitcastingRecorderService> _logger;

    public override string DownloaderName => "twitcastingrecorder";

    public TwitcastingRecorderService(
        ILogger<TwitcastingRecorderService> logger,
        k8s.Kubernetes kubernetes,
        IOptions<KubernetesOption> options,
        IOptions<ServiceOption> serviceOptions,
        IOptions<AzureOption> azureOptions,
        IOptions<NFSOption> nfsOptions) : base(logger, kubernetes, options, serviceOptions, azureOptions, nfsOptions)
    {
        _logger = logger;
    }

    protected override Task<V1Job> CreateNewJobAsync(string id,
                                                     string instanceName,
                                                     string channelId,
                                                     bool useCookiesFile = false,
                                                     CancellationToken cancellation = default)
    {
        try
        {
            return doWithImage("ghcr.io/recorder-moe/twitcasting-recorder:latest");
        }
        catch (Exception)
        {
            // Use DockerHub as fallback
            _logger.LogWarning("Failed once, try docker hub as fallback.");
            return doWithImage("recordermoe/twitcasting-recorder:latest");
        }

        Task<V1Job> doWithImage(string imageName)
        {
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
                            value = new string[] {
                                "/usr/bin/dumb-init", "--",
                                "/bin/bash", "-c",
                                $"/bin/bash record_twitcast.sh {channelId} once && mv /download/*.mp4 /fileshare/"
                            }
                        },
                    },
                    deploymentName: instanceName,
                    cancellation: cancellation);
        }
    }
}
