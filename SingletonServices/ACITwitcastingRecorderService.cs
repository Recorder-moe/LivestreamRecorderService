using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices;

public class ACITwitcastingRecorderService : ACIService
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

    protected override Task<ArmOperation<ArmDeploymentResource>> CreateNewInstance(string channelId, string instanceName, CancellationToken cancellation)
        => CreateAzureContainerInstanceAsync(
            template: "ACI_twitcasting_recorder.json",
            parameters: new
            {
                containerName = new
                {
                    value = instanceName
                },
                commandOverrideArray = new
                {
                    value = new string[] {
                        "/usr/bin/dumb-init", "--",
                        "/bin/bash", "-c",
                        $"/bin/bash record_twitcast.sh {channelId} once && mv /download/*.mp4 /fileshare/"
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
