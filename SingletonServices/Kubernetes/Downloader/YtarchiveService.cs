using k8s.Models;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces.Job.Downloader;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices.Kubernetes.Downloader;

public class YtarchiveService(
    ILogger<YtarchiveService> logger,
    k8s.Kubernetes kubernetes,
    IOptions<ServiceOption> serviceOptions,
    IOptions<AzureOption> azureOptions) : KubernetesServiceBase(logger, kubernetes, serviceOptions, azureOptions), IYtarchiveService
{
    public override string Name => IYtarchiveService.Name;

    protected override Task<V1Job> CreateNewJobAsync(string url,
        string instanceName,
        Video video,
        bool useCookiesFile = false,
        CancellationToken cancellation = default)
    {
        if (!url.StartsWith("http")) url = $"https://youtu.be/{NameHelper.ChangeId.VideoId.PlatformType(url, Name)}";

        try
        {
            return doWithImage("ghcr.io/recorder-moe/ytarchive:latest");
        }
        // skipcq: CS-R1008
        catch (Exception)
        {
            // Use DockerHub as fallback
            logger.LogWarning("Failed once, try docker hub as fallback.");
            return doWithImage("recordermoe/ytarchive:latest");
        }

        Task<V1Job> doWithImage(string imageName)
        {
            string filename = NameHelper.GetFileName(video, IYtarchiveService.Name);
            video.Filename = filename;
            string[] command = useCookiesFile
                ?
                [
                    "dumb-init", "--",
                    "sh", "-c",
                    $"ytarchive --add-metadata --merge --retry-frags 30 --thumbnail -o '{filename.Replace(".mp4", "")}' -c /sharedvolume/cookies/{video.ChannelId}.txt '{url}' best && mv *.mp4 /sharedvolume/"
                ]
                :
                [
                    "dumb-init", "--",
                    "sh", "-c",
                    $"ytarchive --add-metadata --merge --retry-frags 30 --thumbnail -o '{filename.Replace(".mp4", "")}' '{url}' best && mv *.mp4 /sharedvolume/"
                ];

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
