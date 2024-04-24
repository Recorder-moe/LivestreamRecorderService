using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Interfaces.Job.Uploader;
using LivestreamRecorderService.Models;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.SingletonServices.ACI.Downloader;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices.ACI.Uploader;

public class AzureUploaderService(
    ILogger<YtdlpService> logger,
    ArmClient armClient,
    IOptions<AzureOption> options) : AciServiceBase(logger, armClient, options), IAzureUploaderService
{
    public override string Name => IAzureUploaderService.Name;
    private readonly AzureOption _azureOption = options.Value;

    protected override Task<ArmOperation<ArmDeploymentResource>> CreateNewJobAsync(
        string _,
        string instanceName,
        Video video,
        bool useCookiesFile = false,
        CancellationToken cancellation = default)
    {
        try
        {
            return doWithImage("ghcr.io/recorder-moe/azure-uploader:latest");
        }
        // skipcq: CS-R1008
        catch (Exception)
        {
            // Use DockerHub as fallback
            logger.LogWarning("Failed once, try docker hub as fallback.");
            return doWithImage("recordermoe/azure-uploader:latest");
        }

        Task<ArmOperation<ArmDeploymentResource>> doWithImage(string imageName)
        {
            return CreateResourceAsync(
                parameters: new
                {
                    dockerImageName = new
                    {
                        value = imageName
                    },
                    containerName = new
                    {
                        value = instanceName
                    },
                    commandOverrideArray = new
                    {
                        value = new[]
                        {
                            "/bin/sh", "-c",
                            $"/app/azure-uploader.sh {video.Filename?.Replace(".mp4", "")}"
                        }
                    },
                    storageAccountName = new
                    {
                        value = _azureOption.FileShare!.StorageAccountName
                    },
                    storageAccountKey = new
                    {
                        value = _azureOption.FileShare!.StorageAccountKey
                    },
                    fileshareVolumeName = new
                    {
                        value = _azureOption.FileShare.ShareName
                    },
                    environmentVariables = new
                    {
                        value = new List<EnvironmentVariable>
                        {
                            new("STORAGE_ACCOUNT_NAME", _azureOption.BlobStorage!.StorageAccountName, null),
                            new("STORAGE_ACCOUNT_KEY", null, _azureOption.BlobStorage.StorageAccountKey),
                            new("CONTAINER_NAME", _azureOption.BlobStorage.BlobContainerName_Private, null),
                            new("DESTINATION_DIRECTORY", null, "/videos")
                        }
                    }
                },
                deploymentName: instanceName,
                templateName: "ACI_env.json",
                cancellation: cancellation);
        }
    }
}
