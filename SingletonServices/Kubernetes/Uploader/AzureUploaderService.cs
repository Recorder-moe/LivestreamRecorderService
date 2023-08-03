using k8s.Models;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Interfaces.Job.Uploader;
using LivestreamRecorderService.Models;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.SingletonServices.Kubernetes.Downloader;
using Microsoft.Extensions.Options;
using System.Configuration;

namespace LivestreamRecorderService.SingletonServices.Kubernetes.Uploader;

public class AzureUploaderService : KubernetesServiceBase, IAzureUploaderService
{
    private readonly ILogger<YtdlpService> _logger;

    public override string Name => IAzureUploaderService.name;
    private readonly AzureOption _azureOption;

    public AzureUploaderService(
        ILogger<YtdlpService> logger,
        k8s.Kubernetes kubernetes,
        IOptions<KubernetesOption> options,
        IOptions<ServiceOption> serviceOptions,
        IOptions<AzureOption> azureOptions) : base(logger, kubernetes, options, serviceOptions, azureOptions)
    {
        _logger = logger;
        _azureOption = azureOptions.Value;
        if (null == _azureOption.BlobStorage
           || string.IsNullOrEmpty(_azureOption.BlobStorage.BlobContainerName_Private))
        {
            throw new ConfigurationErrorsException("Azure Blob Storage is not configured.");
        }
    }

    protected override Task<V1Job> CreateNewJobAsync(string _,
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

        Task<V1Job> doWithImage(string imageName)
        {
            return CreateInstanceAsync(
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
                            value = new string[] {
                                "/bin/sh", "-c",
                                $"/app/azure-uploader.sh {video.Filename?.Replace(".mp4", "")}"
                            }
                        }
                    },
                    deploymentName: instanceName,
                    environment: new List<EnvironmentVariable>
                    {
                        new EnvironmentVariable("STORAGE_ACCOUNT_NAME", _azureOption.BlobStorage!.StorageAccountName, null),
                        new EnvironmentVariable("STORAGE_ACCOUNT_KEY", null, _azureOption.BlobStorage.StorageAccountKey),
                        new EnvironmentVariable("CONTAINER_NAME", _azureOption.BlobStorage.BlobContainerName_Private, null),
                        new EnvironmentVariable("DESTINATION_DIRECTORY", null, "/videos")
                    },
                    cancellation: cancellation);
        }
    }
}
