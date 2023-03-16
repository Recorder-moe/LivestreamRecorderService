using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices;

public class ACIYtdlpService : ACIService
{
    private readonly AzureOption _azureOption;
    private readonly ILogger<ACIYtdlpService> _logger;

    public override string DownloaderName => "ytdlp";

    public ACIYtdlpService(
        ILogger<ACIYtdlpService> logger,
        ArmClient armClient,
        IOptions<AzureOption> options) : base(logger, armClient, options)
    {
        _azureOption = options.Value;
        _logger = logger;
    }

    public override Task<dynamic> StartInstanceAsync(string videoId, string _ = "", CancellationToken cancellation = default)
        => StartInstanceAsync($"https://youtu.be/{videoId}", cancellation);

    public async Task<dynamic> StartInstanceAsync(string url, CancellationToken cancellation = default)
        => await CreateNewInstance(url: url,
                                   instanceName: GetInstanceName(url),
                                   cancellation: cancellation);

    protected override Task<ArmOperation<ArmDeploymentResource>> CreateNewInstance(string url, string instanceName, CancellationToken cancellation)
    {
        try
        {
            return doWithImage("ghcr.io/recorder-moe/yt-dlp:2023.02.17");
        }
        catch (Exception)
        {
            // Use DockerHub as fallback
            _logger.LogWarning("Failed once, try docker hub as fallback.");
            return doWithImage("recorder-moe/yt-dlp:2023.02.17");
        }

        Task<ArmOperation<ArmDeploymentResource>> doWithImage(string imageName)
        {
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
                            value = new string[] {
                                "dumb-init", "--",
                                "sh", "-c",
                                // It is possible for Youtube to use "-" at the beginning of an id, which can cause errors when using the id as a file name.
                                // Therefore, we add "_" before the file name to avoid such issues.
                                $"yt-dlp --ignore-config --retries 30 --concurrent-fragments 16 --merge-output-format mp4 -S '+codec:h264' --embed-thumbnail --embed-metadata --no-part -o '_%(id)s.%(ext)s' '{url}' && mv *.mp4 /fileshare/"
                            }
                        },
                        storageAccountName = new
                        {
                            value = _azureOption.StorageAccountName
                        },
                        storageAccountKey = new
                        {
                            value = _azureOption.StorageAccountKey
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
