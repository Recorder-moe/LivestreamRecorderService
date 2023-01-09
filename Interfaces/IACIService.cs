using Azure.ResourceManager;
using Azure.ResourceManager.Resources;

namespace LivestreamRecorderService.Interfaces;

public interface IACIService
{
    ArmClient ArmClient { get; }
    string ResourceGroupName { get; }

    Task<ArmOperation<ArmDeploymentResource>> CreateAzureContainerInstanceAsync(string template, dynamic parameters, string deploymentName, CancellationToken cancellation = default);
    Task<ResourceGroupResource> GetResourceGroupAsync(CancellationToken cancellation = default);
}