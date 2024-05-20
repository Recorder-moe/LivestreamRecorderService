using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Interfaces.Job.Uploader;
using LivestreamRecorderService.Models;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.SingletonServices.ACI.Downloader;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices.ACI.Uploader;

public class S3UploaderService(
    ILogger<YtdlpService> logger,
    ArmClient armClient,
    IOptions<S3Option> options,
    IOptions<AzureOption> azureOptions) : AciServiceBase(logger, armClient, azureOptions), IS3UploaderService
{
    private readonly AzureOption _azureOptions = azureOptions.Value;
    private readonly S3Option _s3Option = options.Value;

    public override string Name => IS3UploaderService.Name;

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
        // skipcq: CS-R1008
        catch (Exception)
        {
            // Use DockerHub as fallback
            logger.LogWarning("Failed once, try docker hub as fallback.");
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
                        value = new[]
                        {
                            "/bin/sh", "-c",
                            $"/app/s3-uploader.sh {video.Filename?.Replace(".mp4", "")}"
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
                            new("S3_ENDPOINT", $"http{(_s3Option.Secure ? "s" : "")}://{_s3Option.Endpoint}", null),
                            new("S3_ACCESS_KEY", null, _s3Option.AccessKey),
                            new("S3_SECRET_KEY", null, _s3Option.SecretKey),
                            new("DESTINATION_BUCKET", _s3Option.BucketName_Private, null),
                            new("DESTINATION_DIRECTORY", "videos", null)
                        }
                    }
                },
                deploymentName: instanceName,
                cancellation: cancellation);
        }
    }
}
