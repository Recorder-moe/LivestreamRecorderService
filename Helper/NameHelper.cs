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

    public static class ChangeId
    {
        public static class ChannelId
        {
            // Twitcasting channel id may start with '_' which is not allowed in CouchDB. So we add a prefix 'T' to it.
            public static string PlatformType(string channelId, string Platform)
                => Platform switch
                {
                    "Youtube" or IYtarchiveService.name or IYtdlpService.name => channelId,
                    "Twitch" or IStreamlinkService.name => channelId,
                    "Twitcasting" or ITwitcastingRecorderService.name => channelId.TrimStart('T'),
                    "FC2" or IFC2LiveDLService.name => channelId,
                    _ => throw new NotImplementedException(),
                };

            public static string DatabaseType(string channelId, string Platform)
                => Platform switch
                {
                    "Youtube" or IYtarchiveService.name or IYtdlpService.name => channelId,
                    "Twitch" or IStreamlinkService.name => channelId,
                    "Twitcasting" or ITwitcastingRecorderService.name => 'T' + channelId,
                    "FC2" or IFC2LiveDLService.name => channelId,
                    _ => throw new NotImplementedException(),
                };
        }

        public static class VideoId
        {
            // Youtube video id may start with '_' which is not allowed in CouchDB. So we add a prefix 'Y' to it.
            public static string PlatformType(string videoId, string Platform)
                => Platform switch
                {
                    "Youtube" or IYtarchiveService.name or IYtdlpService.name => videoId.TrimStart('Y'),
                    "Twitch" or IStreamlinkService.name => videoId,
                    "Twitcasting" or ITwitcastingRecorderService.name => videoId,
                    "FC2" or IFC2LiveDLService.name => videoId,
                    _ => throw new NotImplementedException(),
                };

            public static string DatabaseType(string videoId, string Platform)
                => Platform switch
                {
                    "Youtube" or IYtarchiveService.name or IYtdlpService.name => 'Y' + videoId,
                    "Twitch" or IStreamlinkService.name => videoId,
                    "Twitcasting" or ITwitcastingRecorderService.name => videoId,
                    "FC2" or IFC2LiveDLService.name => videoId,
                    _ => throw new NotImplementedException(),
                };
        }
    }
}
