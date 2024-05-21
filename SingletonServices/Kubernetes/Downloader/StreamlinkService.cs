using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Interfaces.Job.Downloader;

namespace LivestreamRecorderService.SingletonServices.Kubernetes.Downloader;

public class StreamlinkService(
    ILogger<StreamlinkService> logger,
    k8s.Kubernetes kubernetes,
    IUploaderService uploaderService) : KubernetesServiceBase(logger,
                                                              kubernetes,
                                                              uploaderService), IStreamlinkService
{
    public override string Name => IStreamlinkService.Name;

    public override Task CreateJobAsync(Video video,
                                        bool useCookiesFile = false,
                                        string? url = null,
                                        CancellationToken cancellation = default)
    {
        url ??= $"twitch.tv/{NameHelper.ChangeId.ChannelId.PlatformType(video.ChannelId, Name)}";

        string fileName = NameHelper.GetFileName(video, IStreamlinkService.Name);
        video.Filename = fileName;

        string instanceName = GetInstanceName(video.id);
        const string mountPath = "/download";

        // Record to temp.mp4 then "ffmpeg -movflags +faststart" to final file name.
        string[] command = ["dumb-init", "--", "sh", "-c"];
        string[] args =
        [
            $"streamlink --twitch-disable-ads -o 'temp.mp4' -f '{url}' best && ffmpeg -i temp.mp4 -map 0:v:0 -map 0:a:0 -c copy -movflags +faststart '{fileName}' && rm temp.mp4"
        ];

        return CreateInstanceAsync(deploymentName: instanceName,
                                   containerName: instanceName,
                                   imageName: "streamlink",
                                   fileName: fileName,
                                   command: command,
                                   args: args,
                                   mountPath: mountPath,
                                   cancellation: cancellation);
    }
}
