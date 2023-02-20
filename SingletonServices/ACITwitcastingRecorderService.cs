using Azure.ResourceManager;
using Azure.ResourceManager.ContainerInstance;
using Azure.ResourceManager.Resources;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices;

public class ACITwitcastingRecorderService : ACIService, IACIService
{
    private readonly AzureOption _azureOption;
    private readonly ILogger<ACITwitcastingRecorderService> _logger;
    private readonly ArmClient _armClient;

    public override string DownloaderName => "twitcastingrecorder";

    public ACITwitcastingRecorderService(
        ILogger<ACITwitcastingRecorderService> logger,
        ArmClient armClient,
        IOptions<AzureOption> options) : base(logger, armClient, options)
    {
        _azureOption = options.Value;
        _logger = logger;
        _armClient = armClient;
    }

    public async Task<dynamic> StartInstanceAsync(string channelId, string videoId = "", CancellationToken cancellation = default)
    {
        // ACI部署上需要時間，僅是Start Instance時較省時，能略過docker image pull
        // 同時需注意 BUG#97 的狀況，在「已啟動」的時候部署新的Instance，在「已停止」時直接啟動舊的Instance
        // 使用的ChannelId來做為預設InstanceName
        var instanceNameChannelId = GetInstanceName(channelId);
        var instanceNameVideoId = GetInstanceName(videoId);

        var instance = await GetInstanceByVideoIdAsync(channelId, cancellation);
        if (null != instance && instance.HasData)
        {
            return instance.Data.ProvisioningState switch
            {
                // 啟動舊的預設Instance
                "Succeeded" or "Failed" or "Stopped" => await StartOldACI(instance),
                // 啟動新的Instance
                _ => await CreateNewInstance(channelId, instanceNameVideoId, cancellation),
            };
        }
        else
        {
            _logger.LogWarning("Failed to get ACI instance for {videoId} {name}. A new instance will now be created.", videoId, instanceNameChannelId);
            // 啟動新的預設Instance
            return await CreateNewInstance(channelId, instanceNameChannelId, cancellation);
        }

        async Task<dynamic> StartOldACI(GenericResource instance, int retry = 0)
        {
            _logger.LogInformation("Detect ACI {ACIName} ProvisioningState as {ProvisioningState}", instance.Id, instance.Data.ProvisioningState);
            if (retry > 3)
            {
                _logger.LogError("Retry too many times for {videoId} {ACIName}, create new instance.", videoId, instance.Id);
                return await CreateNewInstance(channelId, instanceNameChannelId, cancellation);
            }

            try
            {
                return await _armClient.GetContainerGroupResource(instance.Id).StartAsync(Azure.WaitUntil.Started, cancellation);
            }
            catch (Azure.RequestFailedException e)
            {
                _logger.LogWarning(e, "Start ACI {ACIName} failed, retry {retry}", instance.Id, ++retry);
                return await StartOldACI(instance, retry);
            }
        }
    }

    private Task<ArmOperation<ArmDeploymentResource>> CreateNewInstance(string channelId, string instanceName, CancellationToken cancellation)
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
