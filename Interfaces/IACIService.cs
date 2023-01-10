using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using LivestreamRecorderService.DB.Models;

namespace LivestreamRecorderService.Interfaces;

public interface IACIService
{
    ArmClient ArmClient { get; }
    string ResourceGroupName { get; }

    Task<ArmOperation<ArmDeploymentResource>> CreateAzureContainerInstanceAsync(string template, dynamic parameters, string deploymentName, CancellationToken cancellation = default);
    Task<GenericResource?> GetInstanceByVideoIdAsync(string videoId, CancellationToken cancellation = default);
    Task<ResourceGroupResource> GetResourceGroupAsync(CancellationToken cancellation = default);

    /// <summary>
    /// RemoveCompletedInstanceContainer
    /// </summary>
    /// <param name="video"></param>
    /// <returns></returns>
    /// <exception cref="Exception">ACI status is FAILED</exception>
    Task RemoveCompletedInstanceContainer(Video video);
}