using k8s.Models;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces.Job.Downloader;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices.Kubernetes.Downloader;

public class YtarchiveService : KubernetesServiceBase, IYtarchiveService
{
    private readonly ILogger<YtarchiveService> _logger;

    public override string Name => IYtarchiveService.name;

    public YtarchiveService(
        ILogger<YtarchiveService> logger,
        k8s.Kubernetes kubernetes,
        IOptions<KubernetesOption> options,
        IOptions<ServiceOption> serviceOptions,
        IOptions<AzureOption> azureOptions) : base(logger, kubernetes, options, serviceOptions, azureOptions)
    {
        _logger = logger;
    }

    protected override Task<V1Job> CreateNewJobAsync(string url,
                                                     string instanceName,
                                                     Video video,
                                                     bool useCookiesFile,
                                                     CancellationToken cancellation)
    {
        if (!url.StartsWith("http")) url = $"https://youtu.be/{url[1..]}";

        try
        {
            return doWithImage("ghcr.io/recorder-moe/ytarchive:master");
        }
        catch (Exception)
        {
            // Use DockerHub as fallback
            _logger.LogWarning("Failed once, try docker hub as fallback.");
            return doWithImage("recordermoe/ytarchive:master");
        }

        Task<V1Job> doWithImage(string imageName)
        {
            string filename = NameHelper.GetFileName(video, IYtarchiveService.name);
            video.Filename = filename;
            string[] command = useCookiesFile
                ? new string[]
                {
                    "/usr/bin/dumb-init", "--",
                    "sh", "-c",
                    $"/ytarchive --add-metadata --merge --retry-frags 30 --thumbnail -o '{filename.Replace(".mp4", "")}' -c /sharedvolume/cookies/{video.ChannelId}.txt '{url}' best && mv *.mp4 /sharedvolume/"
                }
                : new string[] {
                    "/usr/bin/dumb-init", "--",
                    "sh", "-c",
                    $"/ytarchive --add-metadata --merge --retry-frags 30 --thumbnail -o '{filename.Replace(".mp4", "")}' '{url}' best && mv *.mp4 /sharedvolume/"
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
