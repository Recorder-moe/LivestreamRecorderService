using Azure;
using Azure.ResourceManager;
using Azure.ResourceManager.ContainerInstance;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices;

public class ACIService
{
    private readonly ILogger<ACIService> _logger;
    private readonly ArmClient _armClient;
    private readonly string _resourceGroupName;

    /// <summary>
    /// [a-z0-9]([-a-z0-9]*[a-z0-9])?
    /// </summary>
    public virtual string DownloaderName { get; } = "";

    public ACIService(
        ILogger<ACIService> logger,
        ArmClient armClient,
        IOptions<AzureOption> options
    )
    {
        _logger = logger;
        _armClient = armClient;
        _resourceGroupName = options.Value.ResourceGroupName;
    }

    public async Task<ResourceGroupResource> GetResourceGroupAsync(CancellationToken cancellation = default)
    {
        var subscriptionResource = await _armClient.GetDefaultSubscriptionAsync(cancellation);
        return await subscriptionResource.GetResourceGroupAsync(_resourceGroupName, cancellation);
    }

    public async Task<ArmOperation<ArmDeploymentResource>> CreateAzureContainerInstanceAsync(
        string template,
        dynamic parameters,
        string deploymentName,
        CancellationToken cancellation = default)
    {
        var resourceGroupResource = await GetResourceGroupAsync(cancellation);
        var armDeploymentCollection = resourceGroupResource.GetArmDeployments();
        var templateContent = (await System.IO.File.ReadAllTextAsync(Path.Combine("ARMTemplate", template), cancellation)).TrimEnd();
        var deploymentContent = new ArmDeploymentContent(new ArmDeploymentProperties(ArmDeploymentMode.Incremental)
        {
            Template = BinaryData.FromString(templateContent),
            Parameters = BinaryData.FromObjectAsJson(parameters),
        });
        return await armDeploymentCollection.CreateOrUpdateAsync(waitUntil: WaitUntil.Completed,
                                                                 deploymentName: $"{deploymentName}",
                                                                 content: deploymentContent,
                                                                 cancellationToken: cancellation);
    }

    public async Task<GenericResource?> GetInstanceByVideoIdAsync(string videoId, CancellationToken cancellation = default)
    {
        var resourceGroupResource = await GetResourceGroupAsync(cancellation);
        return resourceGroupResource.GetGenericResources(
                                        filter: $"substringof('{GetInstanceName(videoId)}', name) and resourceType eq 'microsoft.containerinstance/containergroups'",
                                        expand: "provisioningState",
                                        top: 1,
                                        cancellationToken: cancellation)
                                    .FirstOrDefault();
    }

    /// <summary>
    /// RemoveCompletedInstanceContainer
    /// </summary>
    /// <param name="video"></param>
    /// <returns></returns>
    /// <exception cref="Exception">ACI status is FAILED</exception>
    public async Task RemoveCompletedInstanceContainerAsync(Video video, CancellationToken cancellation = default)
    {
        var id = video.Source == "Twitch" 
            ? video.id.TrimStart('v') 
            : video.id;
        var instance = (await GetInstanceByVideoIdAsync(id, cancellation));
        if (null != instance && instance.HasData)
        {
            switch (instance.Data.ProvisioningState)
            {
                case "Succeeded":
                    await instance.DeleteAsync(Azure.WaitUntil.Completed, cancellation);
                    _logger.LogInformation("Delete ACI {aciName} for video {videoId}", instance.Data.Name, video.id);
                    break;
                case "Failed":
                    _logger.LogError("ACI status FAILED! {videoId} {aciname}", video.id, instance.Data.Name);
                    throw new Exception($"ACI status FAILED! {instance.Data.Name}");
                default:
                    _logger.LogWarning("ACI status unhandled! {videoId} {aciname} {ProvisioningState}", video.id, instance.Data.Name, instance.Data.ProvisioningState);
                    break;
            }
        }
        else
        {
            _logger.LogWarning("Failed to get ACI instance for {videoId} {name}. Please check if the ACI exists.", video.id, GetInstanceName(video.id));
        }
    }

    protected string GetInstanceName(string videoId)
        => DownloaderName + videoId.Split("/").Last()
                                   .Split("?").First()
                                   .Split(".").First()
                                   .ToLower()
                                   .Replace("_", "")
                                   .Replace(":", "");

    public async Task<bool> IsACIFailedAsync(Video video, CancellationToken cancellation)
    {
        string ACIName = video.Source == "Twitch"
                            ? video.id.TrimStart('v')
                            : video.id;
        var instance = (await GetInstanceByVideoIdAsync(ACIName, cancellation));
        if (null == instance || !instance.HasData)
        {
            ACIName = video.ChannelId;
            instance = (await GetInstanceByVideoIdAsync(ACIName, cancellation));
            if (null == instance || !instance.HasData)
            {
                _logger.LogError("Failed to get ACI instance for {videoId} {name} when checking ACI IsFailed. Please check if the ACI exists.", video.id, ACIName);
                return true;
            }
        }

        ACIName = instance.Data.Name;
        if (instance.Data.ProvisioningState == "Failed")
        {
            _logger.LogError("ACI status FAILED! {videoId} {aciname}", video.id, ACIName);
            return true;
        }
        else
        {
            return false;
        }
    }

    public virtual async Task<dynamic> StartInstanceAsync(string videoId,
                                                          string channelId,
                                                          bool useCookiesFile = false,
                                                          CancellationToken cancellation = default)
    {
        // ACI部署上需要時間，啟動已存在的Instance較省時
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
                "Succeeded" or "Failed" or "Stopped" => await StartOldACI(instance: instance,
                                                                          channelId: channelId,
                                                                          videoId: videoId,
                                                                          useCookiesFile: useCookiesFile,
                                                                          cancellation: cancellation),
                // 啟動新的Instance
                _ => await CreateNewInstance(id: videoId,
                                             instanceName: instanceNameVideoId,
                                             channelId: channelId,
                                             useCookiesFile: useCookiesFile,
                                             cancellation: cancellation),
            };
        }
        else
        {
            _logger.LogWarning("Failed to get ACI instance for {videoId} {name}. A new instance will now be created.", videoId, instanceNameChannelId);
            // 啟動新的預設Instance
            return await CreateNewInstance(id: videoId,
                                           instanceName: instanceNameChannelId,
                                           channelId: channelId,
                                           useCookiesFile: useCookiesFile,
                                           cancellation: cancellation);
        }
    }

    protected async Task<dynamic> StartOldACI(GenericResource instance,
                                              string channelId,
                                              string videoId,
                                              string? newInstanceName = null,
                                              int retry = 0,
                                              bool useCookiesFile = false,
                                              CancellationToken cancellation = default)
    {
        newInstanceName ??= GetInstanceName(channelId);

        if (retry > 3)
        {
            _logger.LogError("Retry too many times for {videoId} {ACIName}, create new instance.", videoId, instance.Id);
            return await CreateNewInstance(id: videoId,
                                           instanceName: newInstanceName,
                                           channelId: channelId,
                                           useCookiesFile: useCookiesFile,
                                           cancellation: cancellation);
        }

        try
        {
            _logger.LogInformation("Detect ACI {ACIName} ProvisioningState as {ProvisioningState}", instance.Id, instance.Data.ProvisioningState);
            return await _armClient.GetContainerGroupResource(instance.Id).StartAsync(Azure.WaitUntil.Started, cancellation);
        }
        catch (Azure.RequestFailedException e)
        {
            _logger.LogWarning(e, "Start ACI {ACIName} failed, retry {retry}", instance.Id, ++retry);
            return await StartOldACI(instance: instance,
                                     channelId: channelId,
                                     videoId: videoId,
                                     newInstanceName: newInstanceName,
                                     retry: retry,
                                     useCookiesFile: useCookiesFile,
                                     cancellation: cancellation);
        }
    }

    // Must be override.
    protected virtual Task<ArmOperation<ArmDeploymentResource>> CreateNewInstance(string id,
                                                                                  string instanceName,
                                                                                  string channelId,
                                                                                  bool useCookiesFile, CancellationToken cancellation)
        => throw new NotImplementedException();
}
