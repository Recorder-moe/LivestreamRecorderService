using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices;

public class ACIStreamlinkService : ACIService, IACIService
{
    private readonly AzureOption _azureOption;
    public override string DownloaderName => "streamlink";

    public ACIStreamlinkService(
        ILogger<ACIStreamlinkService> logger,
        ArmClient armClient,
        IOptions<AzureOption> options) : base(logger, armClient, options)
    {
        _azureOption = options.Value;
    }

    public Task<ArmOperation<ArmDeploymentResource>> StartInstanceAsync(string userid, string videoId = "", CancellationToken cancellation = default)
        => CreateAzureContainerInstanceAsync(
            template: "ACI_streamlink.json",
            parameters: new
            {
                containerName = new
                {
                    value = GetInstanceName(userid + videoId)
                },
                commandOverrideArray = new
                {
                    value = new string[] {
                        "/usr/local/bin/streamlink", "--twitch-disable-ads",
                                                     "-o", "/downloads/{id}.mp4",
                                                     "-f",
                                                     "twitch.tv/" + userid,
                                                     "best"
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
            deploymentName: GetInstanceName(userid + videoId),
            cancellation: cancellation);
}
