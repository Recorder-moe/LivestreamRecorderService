using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces.Job.Downloader;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices.ACI.Downloader;

public class YtdlpService(
    ILogger<YtdlpService> logger,
    ArmClient armClient,
    IOptions<AzureOption> options) : AciServiceBase(logger, armClient, options), IYtdlpService
{
    private readonly AzureOption _azureOption = options.Value;

    public override string Name => IYtdlpService.Name;

    protected override Task<ArmOperation<ArmDeploymentResource>> CreateNewJobAsync(
        string url,
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

        Task<ArmOperation<ArmDeploymentResource>> doWithImage(string imageName)
        {
            string filename = NameHelper.GetFileName(video, video.Source);
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
