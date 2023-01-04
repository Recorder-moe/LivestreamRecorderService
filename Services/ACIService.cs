using Azure;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;
using LivestreamRecorderService.Models.Interfaces;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.Services;

public class ACIService : IACIService
{
    public ArmClient ArmClient { get; }
    public string ResourceGroupName { get; }
    public ACIService(
        ArmClient armClient,
        IOptions<AzureOption> options
    )
    {
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
        var templateContent = (await File.ReadAllTextAsync(Path.Combine("ARMTemplate", template), cancellation)).TrimEnd();
        var deploymentContent = new ArmDeploymentContent(new ArmDeploymentProperties(ArmDeploymentMode.Incremental)
        {
            Template = BinaryData.FromString(templateContent),
            Parameters = BinaryData.FromObjectAsJson(parameters),
        });
        return await armDeploymentCollection.CreateOrUpdateAsync(waitUntil: WaitUntil.Started,
                                                                 deploymentName: $"{template}_{deploymentName}",
                                                                 content: deploymentContent,
                                                                 cancellationToken: cancellation);
    }
}
