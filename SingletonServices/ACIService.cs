using Azure;
using Azure.ResourceManager;
using Azure.ResourceManager.ContainerInstance;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Models;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices;

public class AciService(ILogger<AciService> logger,
                        ArmClient armClient,
                        IOptions<AzureOption> options,
                        IUploaderService uploaderService) : IJobService
{
    private const string DefaultRegistry = "ghcr.io/recorder-moe/";
    private const string FallbackRegistry = "recordermoe/";
    private readonly string _resourceGroupName = options.Value.ContainerInstance!.ResourceGroupName;

    public Task<bool> IsJobMissing(Video video, CancellationToken cancellation)
    {
        return IsJobMissing(NameHelper.CleanUpInstanceName(video.id), cancellation);
    }

    public async Task<bool> IsJobMissing(string keyword, CancellationToken cancellation)
    {
        return null == (await GetResourceByKeywordAsync(keyword, cancellation));
    }

    public Task<bool> IsJobSucceededAsync(Video video, CancellationToken cancellation = default)
    {
        return IsJobSucceededAsync(NameHelper.CleanUpInstanceName(video.id), cancellation);
    }

    public async Task<bool> IsJobSucceededAsync(string keyword, CancellationToken cancellation = default)
    {
        ContainerGroupResource? resource = await GetResourceByKeywordAsync(keyword, cancellation);
        return null != resource
               && resource.HasData
               && resource.Data.InstanceView.State == "Succeeded";
    }

    public Task<bool> IsJobFailedAsync(Video video, CancellationToken cancellation = default)
    {
        return IsJobFailedAsync(NameHelper.CleanUpInstanceName(video.id), cancellation);
    }

    public async Task<bool> IsJobFailedAsync(string keyword, CancellationToken cancellation)
    {
        ContainerGroupResource? resource = await GetResourceByKeywordAsync(keyword, cancellation);
        return null != resource
               && (!resource.HasData
                   || resource.Data.InstanceView.State == "Failed");
    }

    /// <summary>
    ///     RemoveCompletedInstanceContainer
    /// </summary>
    /// <param name="video"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    /// <exception cref="Exception">ACI status is FAILED</exception>
    public async Task RemoveCompletedJobsAsync(Video video, CancellationToken cancellation = default)
    {
        ContainerGroupResource? resource = await GetResourceByKeywordAsync(NameHelper.CleanUpInstanceName(video.id), cancellation);
        if (null == resource || !resource.HasData)
        {
            resource = await GetResourceByKeywordAsync(NameHelper.CleanUpInstanceName(video.ChannelId), cancellation);
            if (null == resource || !resource.HasData)
            {
                logger.LogError("Failed to get ACI instance for {videoId} when removing completed jobs. Please check if the ACI exists.", video.id);
                return;
            }
        }

        string? jobName = resource.Data.Name;
        if (await IsJobFailedAsync(video, cancellation))
        {
            logger.LogError("ACI status FAILED! {videoId} {jobName}", video.id, jobName);
            throw new InvalidOperationException($"ACI status FAILED! {jobName}");
        }

        Response status = (await resource.DeleteAsync(WaitUntil.Completed, cancellation)).GetRawResponse();
        if (status.IsError)
        {
            logger.LogError("Failed to delete job {jobName} {videoId} {status}", jobName, video.id, status.ReasonPhrase);
            throw new InvalidOperationException($"Failed to delete job {jobName} {video.id} {status.ReasonPhrase}");
        }

        logger.LogInformation("ACI {jobName} {videoId} removed", jobName, video.id);
    }

    public async Task CreateInstanceAsync(string deploymentName,
                                          string containerName,
                                          string imageName,
                                          string fileName,
                                          string[]? command,
                                          string[]? args = null,
                                          string mountPath = "/sharedvolume",
                                          CancellationToken cancellation = default)
    {
        command ??= [];
        if (null != args && args.Length > 0) command = [.. command, .. args];

        if (command.Length == 0)
            throw new ArgumentNullException(nameof(command), "command cannot be empty.");

        GenericResource? job = await GetJobByKeywordAsync(containerName, cancellation);
        if (null != job && !job.HasData)
        {
            logger.LogError("An already active job found for {imageName}", imageName);
            throw new InvalidOperationException("An already active job found.");
        }

        ArmDeploymentCollection armDeploymentCollection = (await GetResourceGroupAsync(cancellation)).GetArmDeployments();

        string templateContent = (await File.ReadAllTextAsync(Path.Combine("ARMTemplate", "ACI.json"), cancellation)).TrimEnd();

        var parameters = new AciParameters(
            new DockerImageName(DefaultRegistry + imageName),
            new UploaderImageName(DefaultRegistry + uploaderService.Image),
            new ContainerName(containerName),
            new MountPath(mountPath),
            new UploaderCommand(
            [
                "dumb-init", "--", uploaderService.ScriptName, $"{mountPath}/{fileName.Replace(".mp4", "")}"
            ]),
            uploaderService.GetEnvironmentVariables(),
            new CommandOverrideArray(command));

        try
        {
            await armDeploymentCollection.CreateOrUpdateAsync(
                waitUntil: WaitUntil.Started,
                deploymentName: deploymentName,
                content: new ArmDeploymentContent(
                    new ArmDeploymentProperties(ArmDeploymentMode.Incremental)
                    {
                        Template = BinaryData.FromString(templateContent),
                        Parameters = BinaryData.FromObjectAsJson(parameters)
                    }),
                cancellationToken: cancellation);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed once, try fallback registry.");
            parameters.DockerImageName.value = FallbackRegistry + imageName;
            parameters.UploaderImageName.value = FallbackRegistry + uploaderService.Image;

            try
            {
                await armDeploymentCollection.CreateOrUpdateAsync(
                    waitUntil: WaitUntil.Started,
                    deploymentName: deploymentName,
                    content: new ArmDeploymentContent(
                        new ArmDeploymentProperties(ArmDeploymentMode.Incremental)
                        {
                            Template = BinaryData.FromString(templateContent),
                            Parameters = BinaryData.FromObjectAsJson(parameters)
                        }),
                    cancellationToken: cancellation);
            }
            catch (Exception e2)
            {
                logger.LogError(e2, "Failed twice, abort.");
                throw;
            }
        }
    }

    private async Task<ContainerGroupResource?> GetResourceByKeywordAsync(string keyword, CancellationToken cancellation = default)
    {
        SubscriptionResource subscriptionResource = await armClient.GetDefaultSubscriptionAsync(cancellation);
        ResourceGroupResource? resourceGroupResource = (await subscriptionResource.GetResourceGroupAsync(_resourceGroupName, cancellation))?.Value;
        ContainerGroupResource? containerGroupResourceTemp =
            resourceGroupResource.GetContainerGroups()
                                 .FirstOrDefault(p => p.Id.Name.Contains(NameHelper.CleanUpInstanceName(keyword)));

        if (null == containerGroupResourceTemp) return null;

        Response<ContainerGroupResource> response =
            (await resourceGroupResource.GetContainerGroupAsync(containerGroupResourceTemp.Id.Name, cancellation));

        return response.HasValue ? response.Value : null;
    }

    private async Task<ResourceGroupResource> GetResourceGroupAsync(CancellationToken cancellation = default)
    {
        SubscriptionResource? subscriptionResource = await armClient.GetDefaultSubscriptionAsync(cancellation);
        return await subscriptionResource.GetResourceGroupAsync(_resourceGroupName, cancellation);
    }

    private async Task<GenericResource?> GetJobByKeywordAsync(string keyword, CancellationToken cancellation = default)
    {
        return (await GetResourceGroupAsync(cancellation))
               // ReSharper disable StringLiteralTypo
               .GetGenericResources(filter: $"substringof('{keyword}', name) and resourceType eq 'microsoft.containerinstance/containergroups'",
                                    expand: "provisioningState",
                                    top: 1,
                                    cancellationToken: cancellation)
               // ReSharper restore StringLiteralTypo
               .FirstOrDefault();
    }
}
