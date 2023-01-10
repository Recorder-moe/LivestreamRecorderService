using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices;

public class ACIYtdlpService : ACIService, IACIService
{
    private readonly AzureOption _azureOption;
    public override string DownloaderName => "ytdlp";

    public ACIYtdlpService(
        ILogger<ACIYtdlpService> logger,
        ArmClient armClient,
        IOptions<AzureOption> options) : base(logger, armClient, options)
    {
        _azureOption = options.Value;
    }

    public Task<ArmOperation<ArmDeploymentResource>> StartInstanceAsync(string url, CancellationToken cancellation = default) 
        => CreateAzureContainerInstanceAsync(
            template: "ACI_ytdlp.json",
            parameters: new
            {
                containerName = new
                {
                    value = GetInstanceName(url)
                },
                commandOverrideArray = new
                {
                    value = new string[] {
                        "dumb-init", "--",
                        "yt-dlp", "--ignore-config",
                                  "--retries", "30",
                                  "--concurrent-fragments", "16",
                                  "--merge-output-format", "mp4",
                                  "-S", "+codec:h264" ,
                                  "--embed-thumbnail",
                                  "--embed-metadata",
                                  "--write-thumbnail",
                                  "--no-part",
                                  "-o", "%(id)s.%(ext)s",
                                  url,
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
            deploymentName: GetInstanceName(url),
            cancellation: cancellation);
}
