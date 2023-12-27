using k8s.Models;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces.Job.Downloader;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices.Kubernetes.Downloader;

public class StreamlinkService(
    ILogger<StreamlinkService> logger,
    k8s.Kubernetes kubernetes,
    IOptions<KubernetesOption> options,
    IOptions<ServiceOption> serviceOptions,
    IOptions<AzureOption> azureOptions) : KubernetesServiceBase(logger, kubernetes, options, serviceOptions, azureOptions), IStreamlinkService
{
    public override string Name => IStreamlinkService.name;

    protected override Task<V1Job> CreateNewJobAsync(string _,
                                                     string instanceName,
                                                     Video video,
                                                     bool useCookiesFile = false,
                                                     CancellationToken cancellation = default)
    {
        try
        {
            return doWithImage("ghcr.io/recorder-moe/streamlink:5.5.1");
        }
        catch (Exception)
        {
            // Use DockerHub as fallback
            logger.LogWarning("Failed once, try docker hub as fallback.");
            return doWithImage("recordermoe/streamlink:5.5.1");
        }

        Task<V1Job> doWithImage(string imageName)
        {
            string filename = NameHelper.GetFileName(video, IStreamlinkService.name);
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
                                "/bin/sh", "-c",
                                $"/usr/local/bin/streamlink --twitch-disable-ads -o '/downloads/{filename}' -f 'twitch.tv/{NameHelper.ChangeId.ChannelId.PlatformType(video.ChannelId, Name)}' best && cd /downloads && for file in *.mp4; do ffmpeg -i \"$file\" -map 0:v:0 -map 0:a:0 -c copy -movflags +faststart 'temp.mp4' && mv 'temp.mp4' \"/sharedvolume/$file\"; done"
                            }
                        },
                    },
                    deploymentName: instanceName,
                    cancellation: cancellation);
        }
    }
}
