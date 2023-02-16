using Azure;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;
using LivestreamRecorderService.DB.Models;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices;

public class ACIService : IACIService
{
    private readonly ILogger<ACIService> _logger;

    public ArmClient ArmClient { get; }
    public string ResourceGroupName { get; }

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
        ArmClient = armClient;
        ResourceGroupName = options.Value.ResourceGroupName;
    }

    public async Task<ResourceGroupResource> GetResourceGroupAsync(CancellationToken cancellation = default)
    {
        var subscriptionResource = await ArmClient.GetDefaultSubscriptionAsync(cancellation);
        return await subscriptionResource.GetResourceGroupAsync(ResourceGroupName, cancellation);
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
        return await armDeploymentCollection.CreateOrUpdateAsync(waitUntil: WaitUntil.Started,
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

    public async Task RemoveCompletedInstanceContainerAsync(Video video, CancellationToken cancellation = default)
    {
        var instance = (await GetInstanceByVideoIdAsync(video.id, cancellation));
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
            _logger.LogWarning("Can not get ACI instance for {videoId} {name}", video.id, GetInstanceName(video.id));
        }
    }

    protected string GetInstanceName(string videoId)
        => DownloaderName + videoId.Split("/").Last()
                                   .Split("?").First()
                                   .Split(".").First()
                                   .ToLower()
                                   .Replace("_", "")
                                   .Replace(":", "");

    public async Task<bool> IsACIFailedAsync(Video video,CancellationToken cancellation)
    {
        var instance = (await GetInstanceByVideoIdAsync(video.id, cancellation));
        if (null == instance || !instance.HasData)
        {
            instance = (await GetInstanceByVideoIdAsync(video.Channel.id, cancellation));
            if (null == instance || !instance.HasData)
            {
                _logger.LogError("Can not get ACI instance for {videoId} when checking ACI IsFailed", video.id);
                return true;
            }
        }

        if (instance.Data.ProvisioningState == "Failed")
        {
            _logger.LogError("ACI status FAILED! {videoId} {aciname}", video.id, instance.Data.Name);
            return true;
        }
        else
        {
            return false;
        }
    }
}
