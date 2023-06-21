using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces.Job.Uploader;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.SingletonServices.ACI.Downloader;
using Microsoft.Extensions.Options;
using System.Configuration;

namespace LivestreamRecorderService.SingletonServices.ACI.Uploader;

public class AzureUploaderService : ACIServiceBase, IAzureUploaderService
{
    private readonly ILogger<YtdlpService> _logger;

    public override string Name => IAzureUploaderService.name;
    private readonly AzureOption _azureOption;
    public AzureUploaderService(
        ILogger<YtdlpService> logger,
        ArmClient armClient,
        IOptions<AzureOption> options) : base(logger, armClient, options)
    {
        _azureOption = options.Value;
        _logger = logger;
    }


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
        catch (Exception)
        {
            // Use DockerHub as fallback
            _logger.LogWarning("Failed once, try docker hub as fallback.");
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
                            value = new string[] { NameHelper.GetFileName(video, video.Source).Replace(".mp4", "") }
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
                        }
                    },
                    deploymentName: instanceName,
                    environment: new Dictionary<string, string>()
                    {
                        {"STORAGE_ACCOUNT_NAME", _azureOption.BlobStorage!.StorageAccountName },
                        {"STORAGE_ACCOUNT_KEY", _azureOption.BlobStorage!.StorageAccountKey },
                        {"CONTAINER_NAME", _azureOption.BlobStorage!.BlobContainerName_Private },
                        {"DESTINATION_DIRECTORY", "/videos" },
                    },
                    cancellation: cancellation);
        }
    }
}
