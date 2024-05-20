using Azure;
using Azure.ResourceManager;
using Azure.ResourceManager.ContainerInstance;
using Azure.ResourceManager.Resources;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces.Job;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices.ACI;

public class AciService(
    ILogger<AciService> logger,
    ArmClient armClient,
    IOptions<AzureOption> options) : IJobService
{
    private readonly string _resourceGroupName = options.Value.ContainerInstance!.ResourceGroupName;

    public Task<bool> IsJobSucceededAsync(Video video, CancellationToken cancellation = default)
        => IsJobSucceededAsync(NameHelper.GetInstanceName(video.id), cancellation);

    public async Task<bool> IsJobSucceededAsync(string keyword, CancellationToken cancellation = default)
    {
        ContainerGroupResource? resource = await GetResourceByKeywordAsync(keyword, cancellation);
        return null != resource && resource.HasData && resource.Data.InstanceView.State == "Succeeded";
    }

    public Task<bool> IsJobFailedAsync(Video video, CancellationToken cancellation = default)
        => IsJobFailedAsync(NameHelper.GetInstanceName(video.id), cancellation);

    public async Task<bool> IsJobFailedAsync(string keyword, CancellationToken cancellation)
    {
        ContainerGroupResource? resource = await GetResourceByKeywordAsync(keyword, cancellation);
        return null == resource || !resource.HasData || resource.Data.InstanceView.State == "Failed";
    }

    /// <summary>
    /// RemoveCompletedInstanceContainer
    /// </summary>
    /// <param name="video"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    /// <exception cref="Exception">ACI status is FAILED</exception>
    public async Task RemoveCompletedJobsAsync(Video video, CancellationToken cancellation = default)
    {
        ContainerGroupResource? resource = await GetResourceByKeywordAsync(NameHelper.GetInstanceName(video.id), cancellation);
        if (null == resource || !resource.HasData)
        {
            resource = await GetResourceByKeywordAsync(NameHelper.GetInstanceName(video.ChannelId), cancellation);
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

    private async Task<ContainerGroupResource?> GetResourceByKeywordAsync(string keyword, CancellationToken cancellation = default)
    {
        SubscriptionResource subscriptionResource = await armClient.GetDefaultSubscriptionAsync(cancellation);
        ResourceGroupResource? resourceGroupResource = (await subscriptionResource.GetResourceGroupAsync(_resourceGroupName, cancellation))?.Value;
        ContainerGroupResource? containerGroupResourceTemp =
            resourceGroupResource.GetContainerGroups()
                                 .FirstOrDefault(p => p.Id.Name.Contains(NameHelper.GetInstanceName(keyword)));

        return null == containerGroupResourceTemp
            ? null
            : (await resourceGroupResource.GetContainerGroupAsync(containerGroupResourceTemp.Id.Name, cancellation)).Value;
    }
}
