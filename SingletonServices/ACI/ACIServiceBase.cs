using Azure;
using Azure.ResourceManager;
using Azure.ResourceManager.ContainerInstance;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces.Job;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices.ACI;

public abstract class ACIServiceBase : IJobServiceBase
{
    private readonly ILogger<ACIServiceBase> _logger;
    private readonly ArmClient _armClient;
    private readonly string _resourceGroupName;

    public ACIServiceBase(
        ILogger<ACIServiceBase> logger,
        ArmClient armClient,
        IOptions<AzureOption> options
    )
    {
        _logger = logger;
        _armClient = armClient;
        _resourceGroupName = options.Value.ContainerInstance!.ResourceGroupName;
    }

    public abstract string Name { get; }

    public virtual async Task InitJobAsync(string videoId,
                                           Video video,
                                           bool useCookiesFile = false,
                                           CancellationToken cancellation = default)
    {
        var jobName = GetInstanceName(videoId);
        var job = await GetJobByKeywordAsync(jobName, cancellation);
        if (null != job && !job.HasData)
        {
            _logger.LogWarning("An active job already exists! Fixed {videoId} status mismatch.", videoId);
            return;
        }

        _logger.LogInformation("Start new ACI job for {videoId} {name}.", videoId, jobName);
        await CreateNewJobAsync(id: videoId,
                                jobName: jobName,
                                video: video,
                                useCookiesFile: useCookiesFile,
                                cancellation: cancellation);
    }

    protected async Task InitJobAsyncWithChannelName(string videoId,
                                                     Video video,
                                                     bool useCookiesFile = false,
                                                     CancellationToken cancellation = default)
    {
        // ACI部署上需要時間，啟動已存在的Instance較省時
        // 同時需注意 BUG#97 的狀況，在「已啟動」的時候部署新的Instance，在「已停止」時直接啟動舊的Instance
        // 使用的ChannelId來做為預設InstanceName
        var instanceNameChannelId = GetInstanceName(video.ChannelId);
        var instanceNameVideoId = GetInstanceName(videoId);

        var job = await GetJobByKeywordAsync(video.ChannelId, cancellation);
        if (null == job || !job.HasData)
        {
            _logger.LogWarning("Does not get ACI instance for {videoId} {name}. A new instance will now be created.", videoId, instanceNameChannelId);
            // 啟動新的Channel Instance
            await CreateNewJobAsync(id: videoId,
                                    jobName: instanceNameChannelId,
                                    video: video,
                                    useCookiesFile: useCookiesFile,
                                    cancellation: cancellation);
            return;
        }

        switch (job.Data.ProvisioningState)
        {
            case "Succeeded":
            case "Failed":
            case "Stopped":
                // 啟動舊的Channel Instance
                await StartOldJob(job: job,
                                  video: video,
                                  useCookiesFile: useCookiesFile,
                                  cancellation: cancellation);
                break;

            default:
                // 啟動新的Video Instance
                await CreateNewJobAsync(id: string.Empty,
                                        jobName: instanceNameVideoId,
                                        video: video,
                                        useCookiesFile: useCookiesFile,
                                        cancellation: cancellation);
                break;
        };
    }

    // Must be override.
    protected abstract Task<ArmOperation<ArmDeploymentResource>> CreateNewJobAsync(
        string id,
        string jobName,
        Video video,
        bool useCookiesFile,
        CancellationToken cancellation);

    protected async Task<ArmOperation<ArmDeploymentResource>> CreateResourceAsync(
        dynamic parameters,
        string deploymentName,
        string? templateName = null,
        CancellationToken cancellation = default)
    {
        var resourceGroupResource = await GetResourceGroupAsync(cancellation);
        var armDeploymentCollection = resourceGroupResource.GetArmDeployments();
        templateName ??= "ACI.json";
        var templateContent = (await File.ReadAllTextAsync(Path.Combine("ARMTemplate", templateName), cancellation)).TrimEnd();
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

    protected async Task<GenericResource?> GetJobByKeywordAsync(string keyword, CancellationToken cancellation = default)
    {
        var resourceGroupResource = await GetResourceGroupAsync(cancellation);
        return resourceGroupResource.GetGenericResources(
                                        filter: $"substringof('{GetInstanceName(keyword)}', name) and resourceType eq 'microsoft.containerinstance/containergroups'",
                                        expand: "provisioningState",
                                        top: 1,
                                        cancellationToken: cancellation)
                                    .FirstOrDefault();
    }

    public string GetInstanceName(string videoId)
        => (Name + NameHelper.GetInstanceName(videoId)).ToLower();

    private async Task StartOldJob(GenericResource job,
                                   Video video,
                                   int retry = 0,
                                   bool useCookiesFile = false,
                                   CancellationToken cancellation = default)
    {
        if (retry > 3)
        {
            _logger.LogError("Retry too many times for {videoId} {ACIName}, create new resource.", video.id, job.Id);
            await CreateNewJobAsync(id: video.id,
                                    jobName: GetInstanceName(video.id),
                                    video: video,
                                    useCookiesFile: useCookiesFile,
                                    cancellation: cancellation);
            return;
        }

        try
        {
            _logger.LogInformation("Detect ACI {ACIName} ProvisioningState as {ProvisioningState}", job.Id, job.Data.ProvisioningState);
            await _armClient.GetContainerGroupResource(job.Id).StartAsync(WaitUntil.Started, cancellation);
        }
        catch (RequestFailedException e)
        {
            _logger.LogWarning(e, "Start ACI {ACIName} failed, retry {retry}", job.Id, ++retry);
            await StartOldJob(job: job,
                              video: video,
                              retry: retry,
                              useCookiesFile: useCookiesFile,
                              cancellation: cancellation);
        }
    }

    private async Task<ResourceGroupResource> GetResourceGroupAsync(CancellationToken cancellation = default)
    {
        var subscriptionResource = await _armClient.GetDefaultSubscriptionAsync(cancellation);
        return await subscriptionResource.GetResourceGroupAsync(_resourceGroupName, cancellation);
    }
}