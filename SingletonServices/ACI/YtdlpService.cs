using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using LivestreamRecorderService.Interfaces.Job;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices.ACI;

public class YtdlpService : ACIServiceBase, IYtdlpService
{
    private readonly AzureOption _azureOption;
    private readonly ILogger<YtdlpService> _logger;

    public override string DownloaderName => "ytdlp";

    public YtdlpService(
        ILogger<YtdlpService> logger,
        ArmClient armClient,
        IOptions<AzureOption> options) : base(logger, armClient, options)
    {
        _azureOption = options.Value;
        _logger = logger;
    }

    public override async Task<dynamic> InitJobAsync(string url,
                                                  string channelId,
                                                  bool useCookiesFile = false,
                                                  CancellationToken cancellation = default)
        => await CreateNewInstance(id: url,
                                   instanceName: GetInstanceName(url),
                                   channelId: channelId,
                                   useCookiesFile: useCookiesFile,
                                   cancellation: cancellation);

    protected override Task<ArmOperation<ArmDeploymentResource>> CreateNewInstance(string id,
                                                                                   string instanceName,
                                                                                   string channelId,
                                                                                   bool useCookiesFile = false,
                                                                                   CancellationToken cancellation = default)
    {
        try
        {
            return doWithImage("ghcr.io/recorder-moe/yt-dlp:2023.03.04");
        }
        catch (Exception)
        {
            // Use DockerHub as fallback
            _logger.LogWarning("Failed once, try docker hub as fallback.");
            return doWithImage("recordermoe/yt-dlp:2023.03.04");
        }

        Task<ArmOperation<ArmDeploymentResource>> doWithImage(string imageName)
        {
            string[] command = useCookiesFile
                ? new string[]
                {
                    "dumb-init", "--",
                    "sh", "-c",
                    $"yt-dlp --ignore-config --retries 30 --concurrent-fragments 16 --merge-output-format mp4 -S '+codec:h264' --embed-thumbnail --embed-metadata --no-part --cookies /fileshare/cookies/{channelId}.txt -o '%(id)s.%(ext)s' '{id}' && mv *.mp4 /fileshare/"
                }
                : new string[]
                {
                    "dumb-init", "--",
                    "sh", "-c",
                    $"yt-dlp --ignore-config --retries 30 --concurrent-fragments 16 --merge-output-format mp4 -S '+codec:h264' --embed-thumbnail --embed-metadata --no-part -o '%(id)s.%(ext)s' '{id}' && mv *.mp4 /fileshare/"
                };

            // Workground for twitcasting ERROR: Initialization fragment found after media fragments, unable to download
            // https://github.com/yt-dlp/yt-dlp/issues/5497
            if (id.Contains("twitcasting.tv"))
            {
                command[4] = command[4].Replace("--ignore-config --retries 30", "--ignore-config --retries 30 --downloader ffmpeg");
            }

            // It is possible for Youtube to use "-" at the beginning of an id, which can cause errors when using the id as a file name.
            // Therefore, we add "_" before the file name to avoid such issues.
            if (id.Contains("youtu"))
            {
                command[4] = command[4].Replace("-o '%(id)s.%(ext)s'", "-o '_%(id)s.%(ext)s'");
            }

            return CreateAzureContainerInstanceAsync(
                    template: "ACI.json",
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
                            value = "livestream-recorder"
                        }
                    },
                    deploymentName: instanceName,
                    cancellation: cancellation);
        }
    }
}
