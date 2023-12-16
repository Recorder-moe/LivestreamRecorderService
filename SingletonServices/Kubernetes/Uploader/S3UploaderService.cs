using k8s.Models;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Interfaces.Job.Uploader;
using LivestreamRecorderService.Models;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.SingletonServices.Kubernetes.Downloader;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices.Kubernetes.Uploader;

public class S3UploaderService(
    ILogger<YtdlpService> logger,
    k8s.Kubernetes kubernetes,
    IOptions<KubernetesOption> options,
    IOptions<ServiceOption> serviceOptions,
    IOptions<S3Option> s3Options,
    IOptions<AzureOption> azureOptions) : KubernetesServiceBase(logger, kubernetes, options, serviceOptions, azureOptions), IS3UploaderService
{
    public override string Name => IS3UploaderService.name;
    private readonly S3Option _s3Option = s3Options.Value;

    protected override Task<V1Job> CreateNewJobAsync(string _,
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
            logger.LogWarning("Failed once, try docker hub as fallback.");
            return doWithImage("recordermoe/s3-uploader:latest");
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
                                $"/app/s3-uploader.sh {video.Filename?.Replace(".mp4", "")}"
                            }
                        }
                    },
                    deploymentName: instanceName,
                    environment: new List<EnvironmentVariable>
                    {
                        new("S3_ENDPOINT", $"http{(_s3Option.Secure? "s": "")}://{_s3Option.Endpoint}", null),
                        new("S3_ACCESS_KEY", null, _s3Option.AccessKey),
                        new("S3_SECRET_KEY", null, _s3Option.SecretKey),
                        new("DESTINATION_BUCKET", _s3Option.BucketName_Private, null),
                        new("DESTINATION_DIRECTORY", "videos", null)
                    },
                    cancellation: cancellation);
        }
    }
}
