using System.Globalization;
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
           .ToLower(CultureInfo.InvariantCulture);

    public static string GetFileName(Video video, string Platform)
        => Platform switch
        {
            "Youtube" or IYtarchiveService.name or IYtdlpService.name => $"{video.id}.mp4",
            "Twitch" or IStreamlinkService.name => $"{video.id}.mp4",
            "Twitcasting" or ITwitcastingRecorderService.name => $"{video.ChannelId}_{DateTime.UtcNow:yyyyMMddHHmmss}.mp4",
            "FC2" or IFC2LiveDLService.name => $"{video.ChannelId}_{DateTime.UtcNow:yyyyMMddHHmmss}.mp4",
            _ => throw new NotImplementedException(),
        };

    /// <summary>
    /// Change the id between platform type and database type. This is designed to prevent id conflict and invalid database key.
    /// </summary>
    public static class ChangeId
    {
        public static class ChannelId
        {
            public static string PlatformType(string channelId, string Platform)
                => Platform switch
                {
                    "Youtube" or IYtarchiveService.name or IYtdlpService.name => channelId, // Youtube channelId already starts with "UC"
                    "Twitch" or IStreamlinkService.name => channelId[2..],
                    "Twitcasting" or ITwitcastingRecorderService.name => channelId[2..],
                    "FC2" or IFC2LiveDLService.name => channelId[2..],
                    _ => throw new NotImplementedException(),
                };

            public static string DatabaseType(string channelId, string Platform)
                => Platform switch
                {
                    "Youtube" or IYtarchiveService.name or IYtdlpService.name => channelId, // Youtube channelId always starts with "UC"
                    "Twitch" or IStreamlinkService.name => "TW" + channelId,
                    "Twitcasting" or ITwitcastingRecorderService.name => "TC" + channelId,
                    "FC2" or IFC2LiveDLService.name => "FC" + channelId,
                    _ => throw new NotImplementedException(),
                };
        }

        public static class VideoId
        {
            public static string PlatformType(string videoId, string Platform)
                => Platform switch
                {
                    "Youtube" or IYtarchiveService.name or IYtdlpService.name => videoId[1..],
                    "Twitch" or IStreamlinkService.name => videoId[2..],
                    "Twitcasting" or ITwitcastingRecorderService.name => videoId[2..],
                    "FC2" or IFC2LiveDLService.name => videoId[2..],
                    _ => throw new NotImplementedException(),
                };

            public static string DatabaseType(string videoId, string Platform)
                => Platform switch
                {
                    "Youtube" or IYtarchiveService.name or IYtdlpService.name => "Y" + videoId,
                    "Twitch" or IStreamlinkService.name => "TW" + videoId,
                    "Twitcasting" or ITwitcastingRecorderService.name => "TC" + videoId,
                    "FC2" or IFC2LiveDLService.name => "FC" + videoId,
                    _ => throw new NotImplementedException(),
                };
        }
    }
}
