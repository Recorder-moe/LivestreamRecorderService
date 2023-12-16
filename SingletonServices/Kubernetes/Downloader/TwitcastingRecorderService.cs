using k8s.Models;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces.Job.Downloader;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices.Kubernetes.Downloader;

public class TwitcastingRecorderService(
    ILogger<TwitcastingRecorderService> logger,
    k8s.Kubernetes kubernetes,
    IOptions<KubernetesOption> options,
    IOptions<ServiceOption> serviceOptions,
    IOptions<AzureOption> azureOptions) : KubernetesServiceBase(logger, kubernetes, options, serviceOptions, azureOptions), ITwitcastingRecorderService
{
    public override string Name => ITwitcastingRecorderService.name;

    protected override Task<V1Job> CreateNewJobAsync(string _,
                                                     string instanceName,
                                                     Video video,
                                                     bool useCookiesFile = false,
                                                     CancellationToken cancellation = default)
    {
        try
        {
            return doWithImage("ghcr.io/recorder-moe/twitcasting-recorder:latest");
        }
        catch (Exception)
        {
            // Use DockerHub as fallback
            logger.LogWarning("Failed once, try docker hub as fallback.");
            return doWithImage("recordermoe/twitcasting-recorder:latest");
        }

        Task<V1Job> doWithImage(string imageName)
        {
            string filename = NameHelper.GetFileName(video, ITwitcastingRecorderService.name);
            video.Filename = filename;
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
                                "dumb-init", "--",
                                "/bin/sh", "-c",
                                $"/bin/sh /app/record_twitcast.sh {video.ChannelId} once -o {Path.GetFileNameWithoutExtension(filename)} && mv /download/*.mp4 /sharedvolume/"
                            }
                        },
                    },
                    deploymentName: instanceName,
                    cancellation: cancellation);
        }
    }
}
