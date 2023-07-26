using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces.Job.Downloader;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices.ACI.Downloader;

public class YtdlpService : ACIServiceBase, IYtdlpService
{
    private readonly AzureOption _azureOption;
    private readonly ILogger<YtdlpService> _logger;

    public override string Name => IYtdlpService.name;

    public YtdlpService(
        ILogger<YtdlpService> logger,
        ArmClient armClient,
        IOptions<AzureOption> options) : base(logger, armClient, options)
    {
        _azureOption = options.Value;
        _logger = logger;
    }

    protected override Task<ArmOperation<ArmDeploymentResource>> CreateNewJobAsync(
        string url,
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

        Task<ArmOperation<ArmDeploymentResource>> doWithImage(string imageName)
        {
            string filename = NameHelper.GetFileName(video, video.Source);
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

            return CreateResourceAsync(
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
                        storageAccountName = new
                        {
                            value = _azureOption.FileShare!.StorageAccountName
                        },
                        storageAccountKey = new
                        {
                            value = _azureOption.FileShare!.StorageAccountKey
                        },
                        fileshareVolumeName = new
                        {
                            value = _azureOption.FileShare!.ShareName
                        },
                    },
                    deploymentName: instanceName,
                    cancellation: cancellation);
        }
    }
}
