using k8s.Models;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces.Job.Downloader;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices.Kubernetes.Downloader;

public class YtdlpService(
    ILogger<YtdlpService> logger,
    k8s.Kubernetes kubernetes,
    IOptions<ServiceOption> serviceOptions,
    IOptions<AzureOption> azureOptions) : KubernetesServiceBase(logger, kubernetes, serviceOptions, azureOptions), IYtdlpService
{
    public override string Name => IYtdlpService.Name;

    protected override Task<V1Job> CreateNewJobAsync(string url,
        string instanceName,
        Video video,
        bool useCookiesFile = false,
        CancellationToken cancellation = default)
    {
        if (!url.StartsWith("http")) url = $"https://youtu.be/{NameHelper.ChangeId.VideoId.PlatformType(url, Name)}";

        try
        {
            return doWithImage("ghcr.io/recorder-moe/yt-dlp:latest");
        }
        // skipcq: CS-R1008
        catch (Exception)
        {
            // Use DockerHub as fallback
            logger.LogWarning("Failed once, try docker hub as fallback.");
            return doWithImage("recordermoe/yt-dlp:latest");
        }

        Task<V1Job> doWithImage(string imageName)
        {
            string filename = NameHelper.GetFileName(video, IYtdlpService.Name);
            video.Filename = filename;
            string[] command = useCookiesFile
                ?
                [
                    "dumb-init", "--",
                    "sh", "-c",
                    $"yt-dlp --ignore-config --retries 30 --concurrent-fragments 16 --merge-output-format mp4 -S '+proto:http,+codec:h264' --embed-thumbnail --embed-metadata --no-part --cookies /sharedvolume/cookies/{video.ChannelId}.txt -o '{filename}' '{url}' && mv *.mp4 /sharedvolume/"
                ]
                :
                [
                    "dumb-init", "--",
                    "sh", "-c",
                    $"yt-dlp --ignore-config --retries 30 --concurrent-fragments 16 --merge-output-format mp4 -S '+proto:http,+codec:h264' --embed-thumbnail --embed-metadata --no-part -o '{filename}' '{url}' && mv *.mp4 /sharedvolume/"
                ];

            // Workaround for twitcasting ERROR: Initialization fragment found after media fragments, unable to download
            // https://github.com/yt-dlp/yt-dlp/issues/5497
            if (url.Contains("twitcasting.tv"))
            {
                command[4] = command[4].Replace("--ignore-config --retries 30", "--ignore-config --retries 30 --downloader ffmpeg");
            }

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
                    }
                },
                deploymentName: instanceName,
                cancellation: cancellation);
        }
    }
}
