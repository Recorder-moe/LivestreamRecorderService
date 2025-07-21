using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces;

namespace LivestreamRecorderService.SingletonServices.Downloader;

public class YtdlpService(IJobService jobService) : IYtdlpService
{
    private static string Name => IYtdlpService.Name;

    public Task CreateJobAsync(Video video,
                               bool useCookiesFile = false,
                               string? url = null,
                               CancellationToken cancellation = default)
    {
        string instanceName = NameHelper.GetInstanceName(Name, video.id);
        url ??= NameHelper.ChangeId.VideoId.PlatformType(video.id, Name);

        if (!url.StartsWith("http"))
            url = $"https://youtu.be/{url}";

        const string mountPath = "/download";
        string fileName = NameHelper.GetFileName(video, Name);
        video.Filename = fileName;
        string[]? command = null;
        string[] args =
        [
            "--ignore-config",
            "--retries", "30",
            "--concurrent-fragments", "16",
            "--merge-output-format", "mp4",
            "-S", "+proto:http,+codec:h264",
            "--embed-thumbnail",
            "--embed-metadata",
            "--no-part",
            "-o", fileName,
            url
        ];

        // Workaround for twitcasting ERROR:
        // Initialization fragment found after media fragments, unable to download
        // https://github.com/yt-dlp/yt-dlp/issues/5497
        if (url.Contains("twitcasting.tv")) args = ["--downloader", "ffmpeg", .. args];

        if (useCookiesFile)
            //args = ["--cookies", $"/cookies/{video.ChannelId}.txt", .. args];
            // Remove this workaround if issue resolved.
            // https://github.com/yt-dlp/yt-dlp/issues/5977#issuecomment-2121742572
            (command, args) = copyCookiesHack();

        return jobService.CreateInstanceAsync(deploymentName: instanceName,
                                              containerName: instanceName,
                                              imageName: "yt-dlp",
                                              fileName: fileName,
                                              command: command,
                                              args: args,
                                              mountPath: mountPath,
                                              cancellation: cancellation);

        (string[] command, string[] args) copyCookiesHack()
        {
            // Cookies file has to be mounted elsewhere and then cp.
            // Because yt-dlp does not support using cookies file in Read-only file system.
            // Which it is how K8s handle secrets.

            // original ENTRYPOINT [ "dumb-init", "--", "sh", "-c", "node /app/build/main.js & exec yt-dlp --no-cache-dir \"$@\"", "sh" ]

            command = ["dumb-init", "--", "sh", "-c"];

            // cp under mountPath to make sure the permission is writable
            args =
            [
                $"cp -r /cookies {mountPath}/cookies && node /app/build/main.js & yt-dlp --ignore-config --retries 30 --concurrent-fragments 16 --merge-output-format mp4 -S '+proto:http,+codec:h264' --embed-thumbnail --embed-metadata --no-part --cookies {mountPath}/cookies/{video.ChannelId}.txt -o '{fileName}' '{url}'"
            ];

            // Workaround for twitcasting ERROR:
            // Initialization fragment found after media fragments, unable to download
            // https://github.com/yt-dlp/yt-dlp/issues/5497
            if (url.Contains("twitcasting.tv")) args[0] = args[0].Replace("--ignore-config", "--ignore-config --downloader ffmpeg");

            return (command, args);
        }
    }
}
