using Azure.ResourceManager;
using Azure.ResourceManager.ContainerInstance;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces.Job;
using LivestreamRecorderService.Interfaces.Job.Uploader;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices.ACI;

public class ACIService(
    ILogger<ACIService> logger,
    ArmClient armClient,
    IOptions<AzureOption> options) : IJobService
{
    private readonly string _resourceGroupName = options.Value.ContainerInstance!.ResourceGroupName!;

    public async Task<bool> IsJobSucceededAsync(Video video, CancellationToken cancellation = default)
        => await IsJobSucceededAsync(video.id, cancellation)
           || await IsJobSucceededAsync(video.ChannelId, cancellation);

    public async Task<bool> IsJobSucceededAsync(string keyword, CancellationToken cancellation = default)
    {
        var resource = await GetResourceByKeywordAsync(NameHelper.GetInstanceName(keyword), cancellation);
        return null != resource && resource.HasData && resource.Data.InstanceView.State == "Succeeded";
    }

    public async Task<bool> IsJobFailedAsync(Video video, CancellationToken cancellation = default)
        => await IsJobFailedAsync(video.id, cancellation)
           && await IsJobFailedAsync(video.ChannelId, cancellation);

    public async Task<bool> IsJobFailedAsync(string keyword, CancellationToken cancellation)
    {
        var resource = await GetResourceByKeywordAsync(NameHelper.GetInstanceName(keyword), cancellation);
        return null == resource || !resource.HasData || resource.Data.InstanceView.State == "Failed";
    }

    /// <summary>
    /// RemoveCompletedInstanceContainer
    /// </summary>
    /// <param name="video"></param>
    /// <returns></returns>
    /// <exception cref="Exception">ACI status is FAILED</exception>
    public async Task RemoveCompletedJobsAsync(Video video, CancellationToken cancellation = default)
    {
        var resource = await GetResourceByKeywordAsync(NameHelper.GetInstanceName(video.id), cancellation);
        if (null == resource || !resource.HasData)
        {
            resource = await GetResourceByKeywordAsync(NameHelper.GetInstanceName(video.ChannelId), cancellation);
            if (null == resource || !resource.HasData)
            {
                logger.LogError("Failed to get ACI instance for {videoId} when removing completed jobs. Please check if the ACI exists.", video.id);
                return;
            }

            if (video.Source is "Twitcasting" or "Twitch" or "FC2"
                     && !resource.Data.Name.StartsWith(IAzureUploaderService.name)
                     && !resource.Data.Name.StartsWith(IS3UploaderService.name))
            {
                logger.LogInformation("Keep ACI {jobName} for video {videoId} platform {platform}", resource.Data.Name, video.id, video.Source);
                return;
            }
        }

        var jobName = resource.Data.Name;
        if (await IsJobFailedAsync(video, cancellation))
        {
            logger.LogError("ACI status FAILED! {videoId} {jobName}", video.id, jobName);
            throw new InvalidOperationException($"ACI status FAILED! {jobName}");
        }

        var status = (await resource.DeleteAsync(Azure.WaitUntil.Completed, cancellation)).GetRawResponse();
        if (status.IsError)
        {
            logger.LogError("Failed to delete job {jobName} {videoId} {status}", jobName, video.id, status.ReasonPhrase);
            throw new InvalidOperationException($"Failed to delete job {jobName} {video.id} {status.ReasonPhrase}");
        }
        logger.LogInformation("Delete ACI {jobName} for video {videoId}", jobName, video.id);
    }

    private async Task<ContainerGroupResource?> GetResourceByKeywordAsync(string keyword, CancellationToken cancellation = default)
    {
        var subscriptionResource = await armClient.GetDefaultSubscriptionAsync(cancellation);
        var resourceGroupResource = (await subscriptionResource.GetResourceGroupAsync(_resourceGroupName, cancellation))?.Value;
        var containerGroupResourceTemp = resourceGroupResource.GetContainerGroups().FirstOrDefault(p => p.Id.Name.Contains(keyword));
        return null == containerGroupResourceTemp
            ? null
            : (await resourceGroupResource.GetContainerGroupAsync(containerGroupResourceTemp.Id.Name, cancellation)).Value;
    }
}
