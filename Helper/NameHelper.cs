using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Interfaces.Job.Downloader;

namespace LivestreamRecorderService.Helper;

public static class NameHelper
{
    public static string GetInstanceName(string id)
        => id.Split("/", StringSplitOptions.RemoveEmptyEntries).Last()
              .Split("?", StringSplitOptions.RemoveEmptyEntries).First()
              .Split(".", StringSplitOptions.RemoveEmptyEntries).First()
              .Replace("_", "")
              .Replace(":", "")
           .ToLower();

    public static string GetFileName(Video video, string Platform)
        => Platform switch
        {
            "Youtube" or IYtarchiveService.name or IYtdlpService.name => $"{video.id}.mp4",
            "Twitch" or IStreamlinkService.name => $"{video.id}.mp4",
            "Twitcasting" or ITwitcastingRecorderService.name => $"{video.ChannelId}_{DateTime.Now:yyyyMMddHHmmss}.mp4",
            "FC2" or IFC2LiveDLService.name => $"{video.ChannelId}_{DateTime.Now:yyyyMMddHHmmss}.mp4",
            _ => throw new NotImplementedException(),
        };
}
