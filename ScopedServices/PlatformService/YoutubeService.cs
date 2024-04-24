#if COSMOSDB
using LivestreamRecorder.DB.CosmosDB;
#elif COUCHDB
using LivestreamRecorder.DB.CouchDB;
#endif
using System.Globalization;
using CodeHollow.FeedReader;
using LivestreamRecorder.DB.Enums;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Interfaces.Job.Downloader;
using LivestreamRecorderService.Models;
using Microsoft.Extensions.Options;
using Serilog.Context;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Models.Options;

namespace LivestreamRecorderService.ScopedServices.PlatformService;

public class YoutubeService(
    ILogger<YoutubeService> logger,
    // ReSharper disable once SuggestBaseTypeForParameterInConstructor
    UnitOfWork_Public unitOfWorkPublic,
    IVideoRepository videoRepository,
    IChannelRepository channelRepository,
    RssService rSsService,
    IStorageService storageService,
    // ReSharper disable once SuggestBaseTypeForParameterInConstructor
    IYtarchiveService ytarchiveService,
    IHttpClientFactory httpClientFactory,
    IOptions<DiscordOption> discordOptions,
    IServiceProvider serviceProvider) : PlatformService(channelRepository,
    storageService,
    httpClientFactory,
    logger,
    discordOptions,
    serviceProvider), IPlatformService
{
#pragma warning disable CA1859 // 盡可能使用具象類型以提高效能
    private readonly IUnitOfWork _unitOfWorkPublic = unitOfWorkPublic;
#pragma warning restore CA1859 // 盡可能使用具象類型以提高效能

    public override string PlatformName => "Youtube";

    public override int Interval => 5 * 60;

    private static string GetRssFeed(Channel channel)
        => $"https://www.youtube.com/feeds/videos.xml?channel_id={NameHelper.ChangeId.ChannelId.PlatformType(channel.id, "Youtube")}";

    private record Item(string Id,
        string Title = "",
        DateTime? Date = null);

    public override async Task UpdateVideosDataAsync(Channel channel, CancellationToken cancellation = default)
    {
        using IDisposable _ = LogContext.PushProperty("Platform", PlatformName);
        using IDisposable __ = LogContext.PushProperty("channelId", channel.id);

        Feed? feed = await rSsService.ReadRssAsync(GetRssFeed(channel), cancellation);

        if (null == feed)
        {
            logger.LogError("Failed to get feed: {channel}", channel.ChannelName);
            return;
        }

        //_rSSService.UpdateChannelName(channel, feed);

        logger.LogTrace("Get {count} videos for channel {channelId} from RSS feed.", feed.Items.Count, channel.id);

        List<Item> items = [];
        feed.Items.ToList().ForEach(item => items.Add(new Item(item.Id, item.Title, item.PublishingDate)));

        if (channel.UseCookiesFile == true)
        {
            string[]? ids = await GetVideoIdsFromMemberPlaylist(channel.id, cancellation);

            logger.LogTrace("Get {count} videos for channel {channelId} from Member Playlist.", feed.Items.Count, channel.id);
            ids?.ToList().ForEach(id => items.Add(new Item(id)));

            items = items.Distinct().ToList();
        }

        foreach (Item item in items)
        {
            await AddOrUpdateVideoAsync(channel, item, cancellation);
        }
    }

    private Task<string[]?> GetVideoIdsFromMemberPlaylist(string channelId, CancellationToken cancellation)
        => GetVideoIdsByYtdlpAsync(
            url: $"https://www.youtube.com/playlist?list=UUMO{NameHelper.ChangeId.ChannelId.PlatformType(channelId, PlatformName)[2..]}",
            limit: 15,
            cancellation: cancellation);

    private Task<YtdlpVideoData?> GetChannelInfoByYtdlpAsync(string channelId, CancellationToken cancellation = default)
        => GetVideoInfoByYtdlpAsync(
            url: $"https://www.youtube.com/channel/{NameHelper.ChangeId.ChannelId.PlatformType(channelId, PlatformName)}",
            cancellation: cancellation);

    /// <summary>
    /// Update video info from RSS feed item. (Which are in Scheduled and Unknown states.)
    /// </summary>
    /// <param name="channel"></param>
    /// <param name="item"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    private async Task AddOrUpdateVideoAsync(Channel channel, Item item, CancellationToken cancellation = default)
    {
        string videoId = NameHelper.ChangeId.VideoId.DatabaseType(item.Id.Split(':').Last(), PlatformName);
        using IDisposable _ = LogContext.PushProperty("videoId", videoId);
        Video? video = await videoRepository.GetVideoByIdAndChannelIdAsync(videoId, channel.id);

        if (null != video)
        {
            // Don't need to track anymore.
            if (video.Status > VideoStatus.Scheduled)
            {
                logger.LogTrace("Video {videoId} is skipped. It is {videoStatus}.", videoId, Enum.GetName(typeof(VideoStatus), video.Status));
                return;
            }
        }
        else
        {
            video = new Video()
            {
                id = videoId,
                Source = PlatformName,
                Status = VideoStatus.Unknown,
                SourceStatus = VideoStatus.Unknown,
                Title = item.Title,
                ChannelId = channel.id,
                Timestamps = new Timestamps()
                {
                    PublishedAt = item.Date
                },
            };

            logger.LogInformation("Found a new Video {videoId} from {channelId}", videoId, channel.id);
        }

        await UpdateVideoDataAsync(video, cancellation);
    }

    /// <summary>
    /// Update video data.
    /// </summary>
    /// <param name="video"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    public override async Task UpdateVideoDataAsync(Video video, CancellationToken cancellation = default)
    {
        using IDisposable __ = LogContext.PushProperty("videoId", video.id);
        YtdlpVideoData? videoData =
            await GetVideoInfoByYtdlpAsync($"https://youtu.be/{NameHelper.ChangeId.VideoId.PlatformType(video.id, PlatformName)}", cancellation);

        if (null == videoData)
        {
            logger.LogWarning("Failed to get video data for {videoId}", video.id);
            video.Status = VideoStatus.Unknown;
            return;
        }

        // Note: The channel can be null by design.
        Channel? channel = await ChannelRepository.GetChannelByIdAndSourceAsync(video.ChannelId, video.Source);

        // Download thumbnail for new videos
        if (video.Status == VideoStatus.Unknown
            || (video.Status == VideoStatus.Pending && string.IsNullOrEmpty(video.Thumbnail)))
        {
            video.Thumbnail = await DownloadThumbnailAsync(videoData.Thumbnail, video.id, cancellation);
        }

        switch (videoData.LiveStatus)
        {
            case "is_upcoming":
                // Premiere video
                if (null != videoData.Duration)
                {
                    video.IsLiveStream = false;

                    if (channel?.SkipNotLiveStream == true)
                    {
                        video.Note = "Video skipped because it is not live stream.";
                        // First detected
                        if (video.Status != VideoStatus.Skipped
                            && null != DiscordService)
                        {
                            await DiscordService.SendSkippedMessageAsync(video, channel);
                        }

                        video.Status = VideoStatus.Skipped;
                        logger.LogInformation("Change video {videoId} status to {videoStatus}",
                            video.id,
                            Enum.GetName(typeof(VideoStatus), video.Status));

                        break;
                    }
                }

                // New stream published
                video.Status = VideoStatus.Scheduled;
                video.Timestamps.ScheduledStartTime =
                    DateTimeOffset.FromUnixTimeSeconds(videoData.ReleaseTimestamp ?? 0).UtcDateTime;

                break;
            case "is_live":
                // Premiere video
                if (null != videoData.Duration)
                {
                    video.Status = VideoStatus.Pending;
                    break;
                }

                // Stream started
                if (video.Status != VideoStatus.Recording)
                {
                    video.Status = VideoStatus.WaitingToRecord;
                    video.Thumbnail = await DownloadThumbnailAsync(videoData.Thumbnail, video.id, cancellation);
                }

                goto case "_live";
            case "post_live":
                // Livestream is finished but cannot download yet.
                if (video.Status != VideoStatus.Recording)
                    video.Status = VideoStatus.Pending;

                logger.LogWarning(
                    "Video {videoId} is currently in post_live status. Please wait for YouTube to prepare the video for download. If the admin still wants to download it, please manually change the video status to \"WaitingToDownload\".",
                    video.id);

                goto case "_live";
            // skipcq: CS-W1001
            case "was_live":
                switch (video.Status)
                {
                    // Old unarchived streams.
                    // Will fall in here when adding a new channel.
                    case VideoStatus.Unknown:
                        video.Status = VideoStatus.Expired;
                        video.Note = "Video expired because it is an old live stream.";
                        logger.LogInformation("Change video {videoId} status to {videoStatus}",
                            video.id,
                            Enum.GetName(typeof(VideoStatus), video.Status));

                        video.Thumbnail = await DownloadThumbnailAsync(videoData.Thumbnail, video.id, cancellation);
                        break;
                    // Should record these streams but not recorded.
                    // Download them.
                    case VideoStatus.Scheduled:
                    case VideoStatus.Pending:
                    case VideoStatus.WaitingToRecord:
                    case VideoStatus.Missing:
                        video.Status = VideoStatus.WaitingToDownload;
                        logger.LogInformation("Change video {videoId} status to {videoStatus}",
                            video.id,
                            Enum.GetName(typeof(VideoStatus), video.Status));

                        video.Thumbnail = await DownloadThumbnailAsync(videoData.Thumbnail, video.id, cancellation);
                        break;
                }

                goto case "_live";
            case "_live":
                video.IsLiveStream = true;

                video.Timestamps.ActualStartTime =
                    DateTimeOffset.FromUnixTimeSeconds(videoData.ReleaseTimestamp ?? 0).UtcDateTime;

                if (null == video.Timestamps.ScheduledStartTime)
                {
                    video.Timestamps.ScheduledStartTime =
                        DateTimeOffset.FromUnixTimeSeconds(videoData.ReleaseTimestamp ?? 0).UtcDateTime;
                }

                break;
            case "not_live":
                video.IsLiveStream = false;

                // Don't download uploaded videos.
                if (video.Status == VideoStatus.Unknown
                    && channel?.SkipNotLiveStream == true)
                {
                    video.Note = "Video skipped because it is not live stream.";
                    // First detected
                    if (video.Status != VideoStatus.Skipped
                        && null != DiscordService)
                    {
                        await DiscordService.SendSkippedMessageAsync(video, channel);
                    }

                    video.Status = VideoStatus.Skipped;
                    logger.LogInformation("Change video {videoId} status to {videoStatus}",
                        video.id,
                        Enum.GetName(typeof(VideoStatus), video.Status));
                }
                else
                {
                    switch (video.Status)
                    {
                        case VideoStatus.Unknown:
                            // New uploaded video.
                            if (DateTime.UtcNow - video.Timestamps.PublishedAt < TimeSpan.FromHours(1))
                            {
                                goto case VideoStatus.Scheduled;
                            }

                            // Old unarchived video.
                            // Will fall in here when adding a new channel.
                            video.Status = VideoStatus.Expired;
                            video.Note = "Video expired because it is an old video.";
                            logger.LogInformation("Change video {videoId} status to {videoStatus}",
                                video.id,
                                Enum.GetName(typeof(VideoStatus), video.Status));

                            video.Thumbnail = await DownloadThumbnailAsync(videoData.Thumbnail, video.id, cancellation);
                            break;
                        // Should download these video but not downloaded.
                        // Download them.
                        case VideoStatus.Scheduled:
                        case VideoStatus.Pending:
                        case VideoStatus.Missing:
                            video.Status = VideoStatus.WaitingToDownload;
                            logger.LogInformation("Change video {videoId} status to {videoStatus}",
                                video.id,
                                Enum.GetName(typeof(VideoStatus), video.Status));

                            video.Thumbnail = await DownloadThumbnailAsync(videoData.Thumbnail, video.id, cancellation);
                            break;

                        case VideoStatus.WaitingToRecord:
                        case VideoStatus.WaitingToDownload:
                        case VideoStatus.Recording:
                        case VideoStatus.Downloading:
                        case VideoStatus.Uploading:
                        case VideoStatus.Archived:
                        case VideoStatus.PermanentArchived:
                        case VideoStatus.Expired:
                        case VideoStatus.Skipped:
                        case VideoStatus.Error:
                        case VideoStatus.Reject:
                        case VideoStatus.Exist:
                        case VideoStatus.Edited:
                        case VideoStatus.Deleted:
                        default:
                            // Don't modify status.
                            break;
                    }
                }

                video.Timestamps.ActualStartTime ??=
                    videoData.ReleaseTimestamp.HasValue
                        ? DateTimeOffset.FromUnixTimeSeconds(videoData.ReleaseTimestamp.Value).UtcDateTime
                        : DateTime.ParseExact(videoData.UploadDate, "yyyyMMdd", CultureInfo.InvariantCulture);

                video.Thumbnail = await DownloadThumbnailAsync(videoData.Thumbnail, video.id, cancellation);
                break;
            default:
                // Deleted
                if (string.IsNullOrEmpty(videoData.LiveStatus)
                    && videoData.Formats?.Count == 0
                    && string.IsNullOrEmpty(videoData.Fulltitle))
                {
                    video.Note = "Get empty video data, maybe it is deleted!";
                    if (video.SourceStatus != VideoStatus.Deleted
                        && video.Status == VideoStatus.Archived)
                    {
                        // First detected
                        video.SourceStatus = VideoStatus.Deleted;
                        if (null != DiscordService)
                        {
                            await DiscordService.SendDeletedMessageAsync(video, channel);
                        }
                    }

                    video.SourceStatus = VideoStatus.Deleted;
                    logger.LogInformation("Get empty video data, maybe it is deleted! {videoId}", video.id);
                }
                else
                {
                    // Unknown
                    goto case "not_live";
                }

                break;
        }

        if (video.SourceStatus != VideoStatus.Deleted)
        {
            video.Title = videoData.Title;
            video.Description = videoData.Description;
        }

        if (string.IsNullOrEmpty(video.Filename)
            || !await StorageService.IsVideoFileExistsAsync(video.Filename, cancellation))
        {
            if (video.SourceStatus == VideoStatus.Deleted
                && video.Status < VideoStatus.Recording)
            {
                video.Note = "This video archive is missing. If you would like to provide it, please contact admin.";
                if (video.Status != VideoStatus.Missing)
                {
                    video.SourceStatus = VideoStatus.Missing;
                    if (null != DiscordService)
                    {
                        await DiscordService.SendSkippedMessageAsync(video, channel);
                    }
                }

                video.Status = VideoStatus.Missing;
                logger.LogWarning("Source removed and not archived, change video status to {status}",
                    Enum.GetName(typeof(VideoStatus), video.Status));
            }

            if (video.Status >= VideoStatus.Archived && video.Status < VideoStatus.Expired)
            {
                video.Status = VideoStatus.Missing;
                video.Note = "Video missing because archived not found.";
                logger.LogWarning("Can not found archived, change video status to {status}", Enum.GetName(typeof(VideoStatus), video.Status));
            }
        }
        else if (video.Status < VideoStatus.Archived || video.Status >= VideoStatus.Expired)
        {
            video.Status = VideoStatus.Archived;
            video.Note = null;
            logger.LogInformation("Correct video status to {status} because archived is exists.", Enum.GetName(typeof(VideoStatus), video.Status));
        }

        switch (videoData.Availability)
        {
            // Member only
            case "subscriber_only":
                if ((channel?.UseCookiesFile) == true)
                {
                    goto case "public";
                }
                else
                {
                    goto case "needs_auth";
                }
            case "public":
            case "unlisted":
                // The source status has been restored from rejection or deletion
                if ((video.SourceStatus == VideoStatus.Reject
                     || video.SourceStatus == VideoStatus.Deleted)
                    // According to observation, the LiveChat of edited videos will be removed.
                    // (But LiveChat can also be manually removed, so it should to be determined after the source status.)
                    && (null == videoData.Subtitles.LiveChat
                        || videoData.Subtitles.LiveChat.Count == 0))
                {
                    video.Note = "Video source is Edited because it has been restored from rejection or deletion.";
                    if (video.SourceStatus != VideoStatus.Edited)
                    {
                        video.SourceStatus = VideoStatus.Edited;
                        if (null != DiscordService)
                        {
                            await DiscordService.SendDeletedMessageAsync(video, channel);
                        }
                    }

                    video.SourceStatus = VideoStatus.Edited;
                    logger.LogInformation("Video source is {status} because it has been restored from rejection or deletion.",
                        Enum.GetName(typeof(VideoStatus), video.SourceStatus));
                }
                else if (video.SourceStatus != VideoStatus.Edited)
                {
                    video.SourceStatus = VideoStatus.Exist;
                }

                break;
            // Copyright Notice
            case "needs_auth":
                // Not archived
                if (video.Status < VideoStatus.Archived)
                {
                    video.Note = "Video is Skipped because it is detected access required or copyright notice.";
                    // First detected
                    if (video.Status != VideoStatus.Skipped
                        && null != DiscordService)
                    {
                        await DiscordService.SendSkippedMessageAsync(video, channel);
                    }

                    video.Status = VideoStatus.Skipped;
                    logger.LogInformation("Video is {status} because it is detected access required or copyright notice.",
                        Enum.GetName(typeof(VideoStatus), video.Status));
                }
                // First detected
                else if (video.SourceStatus != VideoStatus.Reject)
                {
                    video.SourceStatus = VideoStatus.Reject;
                    video.Note = "Video source is detected access required or copyright notice.";
                    if (null != DiscordService)
                    {
                        await DiscordService.SendDeletedMessageAsync(video, channel);
                    }
                }

                video.SourceStatus = VideoStatus.Reject;
                logger.LogInformation("Video source is {status} because it is detected access required or copyright notice.",
                    Enum.GetName(typeof(VideoStatus), video.SourceStatus));

                break;
            default:
                logger.LogWarning("Video {videoId} has a Unknown availability!", video.id);
                break;
        }

        if (video.Status == VideoStatus.WaitingToRecord)
        {
            await ytarchiveService.InitJobAsync(url: video.id,
                video: video,
                useCookiesFile: channel?.UseCookiesFile == true,
                cancellation: cancellation);

            video.Status = VideoStatus.Recording;
            logger.LogInformation("{videoId} is now lived! Start recording.", video.id);
            if (null != DiscordService)
            {
                await DiscordService.SendStartRecordingMessageAsync(video, channel);
            }
        }

        if (video.Status < 0)
            logger.LogError("Video {videoId} has a Unknown status!", video.id);

        await videoRepository.AddOrUpdateAsync(video);
        _unitOfWorkPublic.Commit();
    }

    public override async Task UpdateChannelDataAsync(Channel channel, CancellationToken cancellation)
    {
        string? avatarBlobUrl = channel.Avatar;
        string? bannerBlobUrl = channel.Banner;
        YtdlpVideoData? info = await GetChannelInfoByYtdlpAsync(channel.id, cancellation);
        if (null == info)
        {
            logger.LogWarning("Failed to get channel info for {channelId}", channel.id);
            return;
        }

        var thumbnails = info.Thumbnails.OrderByDescending(p => p.Preference).ToList();
        string? avatarUrl = thumbnails.FirstOrDefault()?.Url;
        if (!string.IsNullOrEmpty(avatarUrl))
        {
            avatarBlobUrl = await DownloadImageAndUploadToBlobStorageAsync(avatarUrl, $"avatar/{channel.id}", cancellation);
        }

        string? bannerUrl = thumbnails.Skip(1).FirstOrDefault()?.Url;
        if (!string.IsNullOrEmpty(bannerUrl))
        {
            bannerBlobUrl = await DownloadImageAndUploadToBlobStorageAsync(bannerUrl, $"banner/{channel.id}", cancellation);
        }

        channel = await ChannelRepository.ReloadEntityFromDBAsync(channel) ?? channel;
        channel.ChannelName = info.Uploader;
        channel.Avatar = avatarBlobUrl?.Replace("avatar/", "");
        channel.Banner = bannerBlobUrl?.Replace("banner/", "");
        await ChannelRepository.AddOrUpdateAsync(channel);
        _unitOfWorkPublic.Commit();
    }
}
