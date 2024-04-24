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

    public static string GetFileName(Video video, string platform)
        => platform switch
        {
            "Youtube" or IYtarchiveService.Name or IYtdlpService.Name => $"{video.id}.mp4",
            "Twitch" or IStreamlinkService.Name => $"{video.id}.mp4",
            "Twitcasting" or ITwitcastingRecorderService.Name => $"{video.ChannelId}_{DateTime.UtcNow:yyyyMMddHHmmss}.mp4",
            "FC2" or IFc2LiveDLService.Name => $"{video.ChannelId}_{DateTime.UtcNow:yyyyMMddHHmmss}.mp4",
            _ => throw new NotImplementedException(),
        };

    /// <summary>
    /// Change the id between platform type and database type. This is designed to prevent id conflict and invalid database key.
    /// </summary>
    public static class ChangeId
    {
        public static class ChannelId
        {
            public static string PlatformType(string channelId, string platform)
                => platform switch
                {
                    "Youtube" or IYtarchiveService.Name or IYtdlpService.Name => channelId, // Youtube channelId already starts with "UC"
                    "Twitch" or IStreamlinkService.Name => channelId[2..],
                    "Twitcasting" or ITwitcastingRecorderService.Name => channelId[2..],
                    "FC2" or IFc2LiveDLService.Name => channelId[2..],
                    _ => throw new NotImplementedException(),
                };

            public static string DatabaseType(string channelId, string platform)
                => platform switch
                {
                    "Youtube" or IYtarchiveService.Name or IYtdlpService.Name => channelId, // Youtube channelId always starts with "UC"
                    "Twitch" or IStreamlinkService.Name => "TW" + channelId,
                    "Twitcasting" or ITwitcastingRecorderService.Name => "TC" + channelId,
                    "FC2" or IFc2LiveDLService.Name => "FC" + channelId,
                    _ => throw new NotImplementedException(),
                };
        }

        public static class VideoId
        {
            public static string PlatformType(string videoId, string platform)
                => platform switch
                {
                    "Youtube" or IYtarchiveService.Name or IYtdlpService.Name => videoId[1..],
                    "Twitch" or IStreamlinkService.Name => videoId[2..],
                    "Twitcasting" or ITwitcastingRecorderService.Name => videoId[2..],
                    "FC2" or IFc2LiveDLService.Name => videoId[2..],
                    _ => throw new NotImplementedException(),
                };

            public static string DatabaseType(string videoId, string platform)
                => platform switch
                {
                    "Youtube" or IYtarchiveService.Name or IYtdlpService.Name => "Y" + videoId,
                    "Twitch" or IStreamlinkService.Name => "TW" + videoId,
                    "Twitcasting" or ITwitcastingRecorderService.Name => "TC" + videoId,
                    "FC2" or IFc2LiveDLService.Name => "FC" + videoId,
                    _ => throw new NotImplementedException(),
                };
        }
    }
}
