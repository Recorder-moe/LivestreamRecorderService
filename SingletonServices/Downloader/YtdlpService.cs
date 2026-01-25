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
                               bool liveFromStart = false,
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
        string[] args = BuildYtdlpArgs(fileName, url, liveFromStart);

        if (useCookiesFile)
            //args = ["--cookies", $"/cookies/{video.ChannelId}.txt", .. args];
            // Remove this workaround if issue resolved.
            // https://github.com/yt-dlp/yt-dlp/issues/5977#issuecomment-2121742572
            (command, args) = BuildCookiesHackCommand(fileName, url, liveFromStart, video.ChannelId, mountPath);

        return jobService.CreateInstanceAsync(deploymentName: instanceName,
                                              containerName: instanceName,
                                              imageName: "yt-dlp",
                                              fileName: fileName,
                                              command: command,
                                              args: args,
                                              mountPath: mountPath,
                                              cancellation: cancellation);
    }

    /// <summary>
    /// Builds yt-dlp command line arguments.
    /// </summary>
    private string[] BuildYtdlpArgs(string fileName, string url, bool liveFromStart)
    {
        // Live-from-start mode uses minimal args to avoid compatibility issues
        // Working example: yt-dlp --live-from-start --no-part -o filename url
        if (liveFromStart)
        {
            return
            [
                "-v",
                "--live-from-start",
                "--embed-thumbnail",
                "--embed-metadata",
                "--no-part",
                "-o", fileName,
                url
            ];
        }

        var args = new List<string>
        {
            "-v",
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
        };

        // Workaround for twitcasting ERROR:
        // Initialization fragment found after media fragments, unable to download
        // https://github.com/yt-dlp/yt-dlp/issues/5497
        if (url.Contains("twitcasting.tv"))
        {
            args.InsertRange(0, ["--downloader", "ffmpeg"]);
        }

        return [.. args];
    }

    /// <summary>
    /// Converts arguments array to shell command string with proper quoting.
    /// </summary>
    private static string ArgsToCommandString(string[] args)
    {
        return string.Join(" ", args.Select(arg =>
            arg.Contains(' ') || arg.Contains('\'') ? $"'{arg.Replace("'", "'\\''")}'" : arg));
    }

    /// <summary>
    /// Builds command for cookies workaround.
    /// Cookies file has to be mounted elsewhere and then copied,
    /// because yt-dlp does not support using cookies file in Read-only file system,
    /// which is how K8s handles secrets.
    /// </summary>
    private (string[] command, string[] args) BuildCookiesHackCommand(
        string fileName,
        string url,
        bool liveFromStart,
        string channelId,
        string mountPath)
    {
        // original ENTRYPOINT: "dumb-init", "--", "yt-dlp", "--no-cache-dir"
        var command = new[] { "dumb-init", "--", "sh", "-c" };

        // Build yt-dlp args and convert to command string
        var ytdlpArgs = BuildYtdlpArgs(fileName, url, liveFromStart);
        var ytdlpCommand = ArgsToCommandString(ytdlpArgs);

        // Insert cookies parameter
        var cookiesPath = $"{mountPath}/cookies/{channelId}.txt";
        ytdlpCommand = ytdlpCommand.Replace("-o ", $"--cookies {cookiesPath} -o ");

        // cp under mountPath to make sure the permission is writable
        var shellScript = $"cp -r /cookies {mountPath}/cookies && yt-dlp {ytdlpCommand}";
        var args = new[] { shellScript };

        return (command, args);
    }
}
