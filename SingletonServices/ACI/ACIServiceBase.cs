using System.Globalization;
using Azure;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Interfaces.Job;
using LivestreamRecorderService.Models;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices.ACI;

public abstract class AciServiceBase(
    ILogger<AciServiceBase> logger,
    ArmClient armClient,
    IOptions<AzureOption> options,
    IUploaderService uploaderService) : IJobServiceBase
{
    private readonly string _resourceGroupName = options.Value.ContainerInstance!.ResourceGroupName;

    public abstract string Name { get; }

    private const string DefaultRegistry = "ghcr.io/recorder-moe/";
    private const string FallbackRegistry = "recordermoe/";

    public virtual async Task CreateJobAsync(Video video,
                                             bool useCookiesFile = false,
                                             string? videoId = null,
                                             CancellationToken cancellation = default)
    {
        string jobName = GetInstanceName(videoId);
        GenericResource? job = await GetJobByKeywordAsync(jobName, cancellation);
        if (null != job && !job.HasData)
        {
            logger.LogWarning("An active job already exists! Fixed {videoId} status mismatch.", videoId);
            return;
        }

        logger.LogInformation("Start new ACI job for {videoId} {name}.", videoId, jobName);
        await CreateNewJobAsync(id: videoId,
                                jobName: jobName,
                                video: video,
                                useCookiesFile: useCookiesFile,
                                cancellation: cancellation);
    }

    private async Task<ResourceGroupResource> GetResourceGroupAsync(CancellationToken cancellation = default)
    {
        SubscriptionResource? subscriptionResource = await armClient.GetDefaultSubscriptionAsync(cancellation);
        return await subscriptionResource.GetResourceGroupAsync(_resourceGroupName, cancellation);
    }

    protected async Task<ArmOperation<ArmDeploymentResource>> CreateResourceAsync(string deploymentName,
                                                                                  string containerName,
                                                                                  string imageName,
                                                                                  string[] args,
                                                                                  string fileName,
                                                                                  IList<EnvironmentVariable>? environment = null,
                                                                                  string mountPath = "/sharedvolume",
                                                                                  bool fallback = false,
                                                                                  CancellationToken cancellation = default)
    {
        ArmDeploymentCollection armDeploymentCollection =
            (await GetResourceGroupAsync(cancellation))
            .GetArmDeployments();

        string templateContent = (await File.ReadAllTextAsync(Path.Combine("ARMTemplate", "ACI.json"), cancellation)).TrimEnd();

        dynamic parameters = new
        {
            dockerImageName = new
            {
                value = fallback switch
                {
                    false => DefaultRegistry + imageName,
                    true => FallbackRegistry + imageName
                },
            },
            uploaderImageName = new
            {
                value = fallback switch
                {
                    false => DefaultRegistry + uploaderService.Image,
                    true => FallbackRegistry + uploaderService.Image
                },
            },
            containerName = new
            {
                value = containerName
            },
            commandOverrideArray = new
            {
                value = args
            },
            mountPath = new
            {
                value = mountPath
            },
            mountPathAndFileName = new
            {
                value = $"{mountPath}/{fileName.Replace(".mp4", "")}"
            },
            environmentVariables = new
            {
                value = environment
            }
        };

        return await armDeploymentCollection.CreateOrUpdateAsync(
            waitUntil: WaitUntil.Started,
            deploymentName: deploymentName,
            content: new ArmDeploymentContent(
                new ArmDeploymentProperties(ArmDeploymentMode.Incremental)
                {
                    Template = BinaryData.FromString(templateContent),
                    Parameters = BinaryData.FromObjectAsJson(parameters),
                }),
            cancellationToken: cancellation);
    }

    private async Task<GenericResource?> GetJobByKeywordAsync(string keyword,
                                                              CancellationToken cancellation = default)
        => (await GetResourceGroupAsync(cancellation))
           // ReSharper disable StringLiteralTypo
           .GetGenericResources(filter: $"substringof('{keyword}', name) and resourceType eq 'microsoft.containerinstance/containergroups'",
                                expand: "provisioningState",
                                top: 1,
                                cancellationToken: cancellation)
           // ReSharper restore StringLiteralTypo
           .FirstOrDefault();

    public string GetInstanceName(string videoId)
        => (Name + NameHelper.GetInstanceName(videoId)).ToLower(CultureInfo.InvariantCulture);

    // Must be overridden.
    protected abstract Task<ArmOperation<ArmDeploymentResource>> CreateNewJobAsync(
        string id,
        string jobName,
        Video video,
        bool useCookiesFile = false,
        CancellationToken cancellation = default);
}
