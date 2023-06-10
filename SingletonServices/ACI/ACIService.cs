using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Interfaces.Job;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices.ACI;

public class ACIService : IJobService
{
    private readonly ILogger<ACIService> _logger;
    private readonly ArmClient _armClient;
    private readonly string _resourceGroupName;

    public ACIService(
        ILogger<ACIService> logger,
        ArmClient armClient,
        IOptions<AzureOption> options
    )
    {
        _logger = logger;
        _armClient = armClient;
        _resourceGroupName = options.Value.AzureContainerInstance!.ResourceGroupName!;
    }

    /// <summary>
    /// RemoveCompletedInstanceContainer
    /// </summary>
    /// <param name="video"></param>
    /// <returns></returns>
    /// <exception cref="Exception">ACI status is FAILED</exception>
    public async Task RemoveCompletedJobsAsync(Video video, CancellationToken cancellation = default)
    {
        var id = video.Source == "Twitch"
            ? video.id.TrimStart('v')
            : video.id;
        var instance = await GetInstanceByKeywordAsync(id, cancellation);
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

    public async Task<bool> IsJobFailedAsync(Video video, CancellationToken cancellation)
    {
        string ACIName = video.Source == "Twitch"
                            ? video.id.TrimStart('v')
                            : video.id;
        var instance = await GetInstanceByKeywordAsync(ACIName, cancellation);
        if (null == instance || !instance.HasData)
        {
            ACIName = video.ChannelId;
            instance = await GetInstanceByKeywordAsync(ACIName, cancellation);
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

    private async Task<GenericResource?> GetInstanceByKeywordAsync(string keyword, CancellationToken cancellation = default)
    {
        var resourceGroupResource = await GetResourceGroupAsync(cancellation);
        return resourceGroupResource.GetGenericResources(
                                        filter: $"substringof('{GetInstanceName(keyword)}', name) and resourceType eq 'microsoft.containerinstance/containergroups'",
                                        expand: "provisioningState",
                                        top: 1,
                                        cancellationToken: cancellation)
                                    .FirstOrDefault();
    }

    private static string GetInstanceName(string videoId)
        => videoId.Split("/").Last()
                              .Split("?").First()
                              .Split(".").First()
                              .Replace("_", "")
                              .Replace(":", "")
           .ToLower();

    private async Task<ResourceGroupResource> GetResourceGroupAsync(CancellationToken cancellation = default)
    {
        var subscriptionResource = await _armClient.GetDefaultSubscriptionAsync(cancellation);
        return await subscriptionResource.GetResourceGroupAsync(_resourceGroupName, cancellation);
    }

}
