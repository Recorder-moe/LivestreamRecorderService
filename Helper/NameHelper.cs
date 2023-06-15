using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Interfaces.Job;

namespace LivestreamRecorderService.Helper;

public static class NameHelper
{
    public static string GetInstanceName(string videoId)
        => (videoId.Split("/").Last()
                              .Split("?").First()
                              .Split(".").First()
                              .Replace("_", "")
                              .Replace(":", "")
           ).ToLower();

    public static string GetFileName(Video video, string Platform)
        => Platform switch
        {
            "Youtube" or IYtarchiveService.downloaderName or IYtdlpService.downloaderName => $"_{video.id}.mp4",
            "Twitch" or IStreamlinkService.downloaderName => $"{video.id}.mp4",
            "Twitcasting" or ITwitcastingRecorderService.downloaderName => $"{video.id}.mp4",
            "FC2" or IFC2LiveDLService.downloaderName => $"{video.ChannelId}_{DateTime.Now:yyyyMMddHHmmss}.mp4",
            _ => throw new NotImplementedException(),
        };
}
