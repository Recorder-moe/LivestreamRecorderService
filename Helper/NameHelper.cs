using System.Globalization;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Interfaces;

namespace LivestreamRecorderService.Helper;

public static class NameHelper
{
    public static string CleanUpInstanceName(string id)
    {
        return id.Split("/", StringSplitOptions.RemoveEmptyEntries).Last()
                 .Split("?", StringSplitOptions.RemoveEmptyEntries).First()
                 .Split(".", StringSplitOptions.RemoveEmptyEntries).First()
                 .Replace("_", "")
                 .Replace(":", "")
                 .ToLower(CultureInfo.InvariantCulture);
    }

    public static string GetInstanceName(string downloaderName, string videoId)
    {
        return (downloaderName + CleanUpInstanceName(videoId)).ToLower(CultureInfo.InvariantCulture);
    }

    public static string GetFileName(Video video, string platform)
    {
        return platform switch
        {
            "Youtube" or IYtarchiveService.Name or IYtdlpService.Name => $"{video.id}.mp4",
            "Twitch" or IStreamlinkService.Name => $"{video.id}.mp4",
            "Twitcasting" or ITwitcastingRecorderService.Name => $"{video.ChannelId}_{DateTime.UtcNow:yyyyMMddHHmmss}.mp4",
            "FC2" or IFc2LiveDLService.Name => $"{video.ChannelId}_{DateTime.UtcNow:yyyyMMddHHmmss}.mp4",
            _ => throw new NotImplementedException()
        };
    }

    /// <summary>
    ///     Change the id between platform type and database type. This is designed to prevent id conflict and invalid database
    ///     key.
    /// </summary>
    public static class ChangeId
    {
        public static class ChannelId
        {
            public static string PlatformType(string channelId, string platform)
            {
                return platform switch
                {
                    "Youtube" or IYtarchiveService.Name or IYtdlpService.Name
                        => channelId, // Youtube channelId already starts with "UC"
                    "Twitch" or IStreamlinkService.Name
                        => channelId.StartsWith("TW") ? channelId[2..] : channelId,
                    "Twitcasting" or ITwitcastingRecorderService.Name
                        => channelId.StartsWith("TC") ? channelId[2..] : channelId,
                    "FC2" or IFc2LiveDLService.Name
                        => channelId.StartsWith("FC") ? channelId[2..] : channelId,
                    _ => throw new NotImplementedException()
                };
            }

            public static string DatabaseType(string channelId, string platform)
            {
                return platform switch
                {
                    "Youtube" or IYtarchiveService.Name or IYtdlpService.Name
                        => channelId, // Youtube channelId always starts with "UC"
                    "Twitch" or IStreamlinkService.Name
                        => channelId.StartsWith("TW") ? channelId : "TW" + channelId,
                    "Twitcasting" or ITwitcastingRecorderService.Name
                        => channelId.StartsWith("TC") ? channelId : "TC" + channelId,
                    "FC2" or IFc2LiveDLService.Name
                        => channelId.StartsWith("FC") ? channelId : "FC" + channelId,
                    _ => throw new NotImplementedException()
                };
            }
        }

        public static class VideoId
        {
            public static string PlatformType(string videoId, string platform)
            {
                return platform switch
                {
                    "Youtube" or IYtarchiveService.Name or IYtdlpService.Name
                        => videoId.TrimStart('Y'),
                    "Twitch" or IStreamlinkService.Name
                        => videoId.StartsWith("TW") ? videoId[2..] : videoId,
                    "Twitcasting" or ITwitcastingRecorderService.Name
                        => videoId.StartsWith("TC") ? videoId[2..] : videoId,
                    "FC2" or IFc2LiveDLService.Name
                        => videoId.StartsWith("FC") ? videoId[2..] : videoId,
                    _ => throw new NotImplementedException()
                };
            }

            public static string DatabaseType(string videoId, string platform)
            {
                return platform switch
                {
                    "Youtube" or IYtarchiveService.Name or IYtdlpService.Name
                        => videoId.StartsWith('Y') ? videoId : "Y" + videoId,
                    "Twitch" or IStreamlinkService.Name
                        => videoId.StartsWith("TW") ? videoId : "TW" + videoId,
                    "Twitcasting" or ITwitcastingRecorderService.Name
                        => videoId.StartsWith("TC") ? videoId : "TC" + videoId,
                    "FC2" or IFc2LiveDLService.Name
                        => videoId.StartsWith("FC") ? videoId : "FC" + videoId,
                    _ => throw new NotImplementedException()
                };
            }
        }
    }
}
