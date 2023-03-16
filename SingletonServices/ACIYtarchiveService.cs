using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices;

public class ACIYtarchiveService : ACIService
{
    private readonly ILogger<ACIYtarchiveService> _logger;
    private readonly AzureOption _azureOption;

    public override string DownloaderName => "ytarchive";

    public ACIYtarchiveService(
        ILogger<ACIYtarchiveService> logger,
        ArmClient armClient,
        IOptions<AzureOption> options) : base(logger, armClient, options)
    {
        _logger = logger;
        _azureOption = options.Value;
    }

    public Task<dynamic> StartInstanceAsync(string videoId, CancellationToken cancellation = default)
        => StartInstanceAsync(videoId, "", cancellation);

    public override async Task<dynamic> StartInstanceAsync(string videoId, string channelId = "", CancellationToken cancellation = default)
    {
        if (null != await GetInstanceByVideoIdAsync(videoId, cancellation))
        {
            _logger.LogWarning("ACI already exists! Fixed {videoId} status mismatch.", videoId);
            return Task.CompletedTask;
        }
        else
        {
            var url = $"https://youtu.be/{videoId}";
            return CreateNewInstance(url, GetInstanceName(url), cancellation);
        }
    }

    protected override Task<ArmOperation<ArmDeploymentResource>> CreateNewInstance(string url, string instanceName, CancellationToken cancellation)
    {
        try
        {
            return doWithImage("ghcr.io/recorder-moe/ytarchive:v0.3.2");
        }
        catch (Exception)
        {
            // Use DockerHub as fallback
            _logger.LogWarning("Failed once, try docker hub as fallback.");
            return doWithImage("recordermoe/ytarchive:v0.3.2");
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
                                "/usr/bin/dumb-init", "--",
                                "sh", "-c",
                                // It is possible for Youtube to use "-" at the beginning of an id, which can cause errors when using the id as a file name.
                                // Therefore, we add "_" before the file name to avoid such issues.
                                $"/ytarchive --add-metadata --merge --retry-frags 30 --thumbnail -o '_%(id)s' '{url}' best && mv *.mp4 /fileshare/"
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
