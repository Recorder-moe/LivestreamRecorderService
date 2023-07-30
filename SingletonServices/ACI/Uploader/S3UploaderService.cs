using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces.Job.Uploader;
using LivestreamRecorderService.Models;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.SingletonServices.ACI.Downloader;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices.ACI.Uploader;

public class S3UploaderService : ACIServiceBase, IS3UploaderService
{
    private readonly ILogger<YtdlpService> _logger;
    private readonly AzureOption _azureOptions;
    private readonly S3Option _s3Option;

    public override string Name => IS3UploaderService.name;
    public S3UploaderService(
        ILogger<YtdlpService> logger,
        ArmClient armClient,
        IOptions<S3Option> options,
        IOptions<AzureOption> azureOptions) : base(logger, armClient, azureOptions)
    {
        _s3Option = options.Value;
        _logger = logger;
        _azureOptions = azureOptions.Value;
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
            return doWithImage("ghcr.io/recorder-moe/s3-uploader:latest");
        }
        catch (Exception)
        {
            // Use DockerHub as fallback
            _logger.LogWarning("Failed once, try docker hub as fallback.");
            return doWithImage("recordermoe/s3-uploader:latest");
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
                            value = new string[] {
                                "/bin/sh", "-c",
                                $"/app/s3-uploader.sh {NameHelper.GetFileName(video, video.Source).Replace(".mp4", "")}"
                            }
                        },
                        storageAccountName = new
                        {
                            value = _azureOptions.FileShare!.StorageAccountName
                        },
                        storageAccountKey = new
                        {
                            value = _azureOptions.FileShare!.StorageAccountKey
                        },
                        fileshareVolumeName = new
                        {
                            value = _azureOptions.FileShare.ShareName
                        },
                        environmentVariables = new
                        {
                            value = new List<EnvironmentVariable>
                            {
                                new EnvironmentVariable("S3_ENDPOINT", _s3Option.Endpoint, null),
                                new EnvironmentVariable("S3_ACCESS_KEY", _s3Option.AccessKey, null),
                                new EnvironmentVariable("S3_SECRET_KEY", _s3Option.SecretKey, null),
                                new EnvironmentVariable("DESTINATION_BUCKET", _s3Option.BucketName_Private, null),
                                new EnvironmentVariable("DESTINATION_DIRECTORY", null, "/videos")
                            }
                        }
                    },
                    deploymentName: instanceName,
                    templateName: "ACI_env.json",
                    cancellation: cancellation);
        }
    }
}
