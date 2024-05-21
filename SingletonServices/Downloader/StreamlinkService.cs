using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces;

namespace LivestreamRecorderService.SingletonServices.Downloader;

public class StreamlinkService(IJobService jobService) : IStreamlinkService
{
    private static string Name => IStreamlinkService.Name;

    public Task CreateJobAsync(Video video,
                               bool useCookiesFile = false,
                               string? url = null,
                               CancellationToken cancellation = default)
    {
        url ??= $"twitch.tv/{NameHelper.ChangeId.ChannelId.PlatformType(video.ChannelId, Name)}";

        string fileName = NameHelper.GetFileName(video, Name);
        video.Filename = fileName;

        string instanceName = NameHelper.GetInstanceName(Name, video.id);
        const string mountPath = "/download";

        // Record to temp.mp4 then "ffmpeg -movflags +faststart" to final file name.
        string[] command = ["dumb-init", "--", "sh", "-c"];
        string[] args =
        [
            $"streamlink --twitch-disable-ads -o 'temp.mp4' -f '{url}' best && ffmpeg -i temp.mp4 -map 0:v:0 -map 0:a:0 -c copy -movflags +faststart '{fileName}' && rm temp.mp4"
        ];

        return jobService.CreateInstanceAsync(deploymentName: instanceName,
                                              containerName: instanceName,
                                              imageName: "streamlink",
                                              fileName: fileName,
                                              command: command,
                                              args: args,
                                              mountPath: mountPath,
                                              cancellation: cancellation);
    }
}
