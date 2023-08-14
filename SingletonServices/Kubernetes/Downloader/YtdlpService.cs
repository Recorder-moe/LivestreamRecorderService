using k8s.Models;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces.Job.Downloader;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices.Kubernetes.Downloader;

public class YtdlpService : KubernetesServiceBase, IYtdlpService
{
    private readonly ILogger<YtdlpService> _logger;

    public override string Name => IYtdlpService.name;

    public YtdlpService(
        ILogger<YtdlpService> logger,
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
                                                     bool useCookiesFile = false,
                                                     CancellationToken cancellation = default)
    {
        try
        {
            return doWithImage("ghcr.io/recorder-moe/yt-dlp:2023.07.06");
        }
        catch (Exception)
        {
            // Use DockerHub as fallback
            _logger.LogWarning("Failed once, try docker hub as fallback.");
            return doWithImage("recordermoe/yt-dlp:2023.07.06");
        }

        Task<V1Job> doWithImage(string imageName)
        {
            string filename = NameHelper.GetFileName(video, IYtdlpService.name);
            video.Filename = filename;
            string[] command = useCookiesFile
                ? new string[]
                {
                    "dumb-init", "--",
                    "sh", "-c",
                    $"yt-dlp --ignore-config --retries 30 --concurrent-fragments 16 --merge-output-format mp4 -S '+codec:h264' --embed-thumbnail --embed-metadata --no-part --cookies /sharedvolume/cookies/{video.ChannelId}.txt -o '{filename}' '{url}' && mv *.mp4 /sharedvolume/"
                }
                : new string[]
                {
                    "dumb-init", "--",
                    "sh", "-c",
                    $"yt-dlp --ignore-config --retries 30 --concurrent-fragments 16 --merge-output-format mp4 -S '+codec:h264' --embed-thumbnail --embed-metadata --no-part -o '{filename}' '{url}' && mv *.mp4 /sharedvolume/"
                };

            // Workground for twitcasting ERROR: Initialization fragment found after media fragments, unable to download
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
