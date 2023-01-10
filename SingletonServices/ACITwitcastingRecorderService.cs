using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices;

public class ACITwitcastingRecorderService : ACIService, IACIService
{
    private readonly AzureOption _azureOption;
    public override string DownloaderName => "twitcastingrecorder";

    public ACITwitcastingRecorderService(
        ILogger<ACITwitcastingRecorderService> logger,
        ArmClient armClient,
        IOptions<AzureOption> options) : base(logger, armClient, options)
    {
        _azureOption = options.Value;
    }

    public Task<ArmOperation<ArmDeploymentResource>> StartInstanceAsync(string channelId, CancellationToken cancellation = default)
        => CreateAzureContainerInstanceAsync(
            template: "ACI_twitcasting_recorder.json",
            parameters: new
            {
                containerName = new
                {
                    value = GetInstanceName(channelId)
                },
                commandOverrideArray = new
                {
                    value = new string[] {
                        "/usr/bin/dumb-init", "--",
                        "/bin/bash", "record_twitcast.sh",
                                     channelId,
                                     "once"
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
            deploymentName: GetInstanceName(channelId),
            cancellation: cancellation);

}
