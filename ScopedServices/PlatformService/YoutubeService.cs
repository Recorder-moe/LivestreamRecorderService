using CodeHollow.FeedReader;
using LivestreamRecorder.DB.Core;
using LivestreamRecorder.DB.Enums;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Interfaces.Job.Downloader;
using LivestreamRecorderService.Models;
using LivestreamRecorderService.Models.OptionDiscords;
using Microsoft.Extensions.Options;
using Serilog.Context;

namespace LivestreamRecorderService.ScopedServices.PlatformService;

public class YoutubeService : PlatformService, IPlatformService
{
    private readonly ILogger<YoutubeService> _logger;
    private readonly IUnitOfWork _unitOfWork_Public;
    private readonly IVideoRepository _videoRepository;
    private readonly IChannelRepository _channelRepository;
    private readonly RSSService _rSSService;
    private readonly IStorageService _storageService;
    private readonly IYtarchiveService _ytarchiveService;

    public override string PlatformName => "Youtube";

    public override int Interval => 5 * 60;

    public YoutubeService(
        ILogger<YoutubeService> logger,
        UnitOfWork_Public unitOfWork_Public,
        IVideoRepository videoRepository,
        IChannelRepository channelRepository,
        RSSService rSSService,
        IStorageService storageService,
        IYtarchiveService ytarchiveService,
        IHttpClientFactory httpClientFactory,
        IOptions<DiscordOption> discordOptions,
        IServiceProvider serviceProvider) : base(channelRepository,
                                                 storageService,
                                                 httpClientFactory,
                                                 logger,
                                                 discordOptions,
                                                 serviceProvider)
    {
        _logger = logger;
        _unitOfWork_Public = unitOfWork_Public;
        _videoRepository = videoRepository;
        _channelRepository = channelRepository;
        _rSSService = rSSService;
        _storageService = storageService;
        _ytarchiveService = ytarchiveService;
    }

    public static string GetRSSFeed(Channel channel)
        => $"https://www.youtube.com/feeds/videos.xml?channel_id={channel.id}";

    public override async Task UpdateVideosDataAsync(Channel channel, CancellationToken cancellation = default)
    {
        using var _ = LogContext.PushProperty("Platform", PlatformName);
        using var __ = LogContext.PushProperty("channelId", channel.id);

        var feed = await _rSSService.ReadRSS(GetRSSFeed(channel), cancellation);

        if (null == feed)
        {
            _logger.LogError("Failed to get feed: {channel}", channel.ChannelName);
            return;
        }

        //_rSSService.UpdateChannelName(channel, feed);

        _logger.LogTrace("Get {count} videos for channel {channelId}", feed.Items.Count, channel.id);
        foreach (var item in feed.Items)
        {
            await AddOrUpdateVideoAsync(channel, item, cancellation);
        }
    }

    private Task<YtdlpVideoData?> GetChannelInfoByYtdlpAsync(string ChannelId, CancellationToken cancellation = default)
        => GetVideoInfoByYtdlpAsync($"https://www.youtube.com/channel/{ChannelId}/about", cancellation);

    /// <summary>
    /// Update video info from RSS feed item. (Which are in Scheduled and Unknown states.)
    /// </summary>
    /// <remarks>!!! Updates will not save to DB !!! Must call SaveChanges yourself !!!</remarks>
    /// <param name="channel"></param>
    /// <param name="item"></param>
    /// <returns></returns>
    private async Task AddOrUpdateVideoAsync(Channel channel, FeedItem item, CancellationToken cancellation = default)
    {
        var videoId = item.Id.Split(':').Last();
        using var _ = LogContext.PushProperty("videoId", videoId);
        var video = _videoRepository.Exists(videoId)
                    ? _videoRepository.GetById(videoId)
                    : null;

        // Don't need to track anymore.
        if (null != video
            && video.Status > VideoStatus.Scheduled)
        {
            _logger.LogTrace("Video {videoId} from RSSFeed is skipped. It is {videoStatus}.", videoId, Enum.GetName(typeof(VideoStatus), video.Status));
            return;
        }

        if (null == video)
        {
            video = new Video()
            {
                id = videoId,
                Source = PlatformName,
                Status = VideoStatus.Unknown,
                SourceStatus = VideoStatus.Unknown,
                Title = item.Title,
                ChannelId = channel.id,
                Channel = channel,
                Timestamps = new Timestamps()
                {
                    PublishedAt = item.PublishingDate
                },
            };
            _logger.LogInformation("Found a new Video {videoId} from {channelId}", videoId, channel.id);
        }
        else
        {
            _videoRepository.LoadRelatedData(video);
        }

        await UpdateVideoDataAsync(video, cancellation);
    }

    /// <summary>
    /// Update video data.
    /// </summary>
    /// <remarks>!!! Updates will not save to DB !!! Must call SaveChanges yourself !!!</remarks>
    /// <param name="video"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    public override async Task UpdateVideoDataAsync(Video video, CancellationToken cancellation = default)
    {
        using var __ = LogContext.PushProperty("videoId", video.id);
        YtdlpVideoData? videoData = await GetVideoInfoByYtdlpAsync($"https://youtu.be/{video.id}", cancellation);

        if (null == videoData)
        {
            _logger.LogWarning("Failed to get video data for {videoId}", video.id);
            video.Status = VideoStatus.Unknown;
            return;
        }

        // Download thumbnail for new videos
        if (video.Status == VideoStatus.Unknown
            || video.Status == VideoStatus.Pending && string.IsNullOrEmpty(video.Thumbnail))
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

                    if (video.Channel?.SkipNotLiveStream == true)
                    {
                        video.Note = $"Video skipped because it is not live stream.";
                        // First detected
                        if (video.Status != VideoStatus.Skipped
                            && null != discordService)
                        {
                            await discordService.SendSkippedMessage(video);
                        }
                        video.Status = VideoStatus.Skipped;
                        _logger.LogInformation("Change video {videoId} status to {videoStatus}", video.id, Enum.GetName(typeof(VideoStatus), video.Status));
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

                _logger.LogWarning("Video {videoId} is currently in post_live status. Please wait for YouTube to prepare the video for download. If the admin still wants to download it, please manually change the video status to \"WaitingToDownload\".", video.id);
                goto case "_live";
            case "was_live":
                switch (video.Status)
                {
                    // Old unarchived streams.
                    // Will fall in here when adding a new channel.
                    case VideoStatus.Unknown:
                        video.Status = VideoStatus.Expired;
                        video.Note = $"Video expired because it is an old live stream.";
                        _logger.LogInformation("Change video {videoId} status to {videoStatus}", video.id, Enum.GetName(typeof(VideoStatus), video.Status));
                        video.Thumbnail = await DownloadThumbnailAsync(videoData.Thumbnail, video.id, cancellation);
                        break;
                    // Should record these streams but not recorded.
                    // Download them.
                    case VideoStatus.Scheduled:
                    case VideoStatus.Pending:
                    case VideoStatus.WaitingToRecord:
                        video.Status = VideoStatus.WaitingToDownload;
                        _logger.LogInformation("Change video {videoId} status to {videoStatus}", video.id, Enum.GetName(typeof(VideoStatus), video.Status));
                        video.Thumbnail = await DownloadThumbnailAsync(videoData.Thumbnail, video.id, cancellation);
                        break;
                    default:
                        // Don't modify status.
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
                    && video.Channel?.SkipNotLiveStream == true)
                {
                    video.Note = $"Video skipped because it is not live stream.";
                    // First detected
                    if (video.Status != VideoStatus.Skipped
                        && null != discordService)
                    {
                        await discordService.SendSkippedMessage(video);
                    }
                    video.Status = VideoStatus.Skipped;
                    _logger.LogInformation("Change video {videoId} status to {videoStatus}", video.id, Enum.GetName(typeof(VideoStatus), video.Status));
                }
                else
                {
                    video.Status = VideoStatus.WaitingToDownload;
                    _logger.LogInformation("Change video {videoId} status to {videoStatus}", video.id, Enum.GetName(typeof(VideoStatus), video.Status));
                }

                video.Timestamps.ActualStartTime ??=
                    videoData.ReleaseTimestamp.HasValue
                        ? DateTimeOffset.FromUnixTimeSeconds(videoData.ReleaseTimestamp.Value).UtcDateTime
                        : DateTime.ParseExact(videoData.UploadDate, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
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
                        if (null != discordService)
                        {
                            await discordService.SendDeletedMessage(video);
                        }
                    }

                    video.SourceStatus = VideoStatus.Deleted;
                    _logger.LogInformation("Get empty video data, maybe it is deleted! {videoId}", video.id);
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

        if (!string.IsNullOrEmpty(video.Filename)
            && !await _storageService.IsVideoFileExists(video.Filename, cancellation))
        {
            if (video.SourceStatus == VideoStatus.Deleted
                && video.Status < VideoStatus.Recording)
            {
                video.Note = "This video archive is missing. If you would like to provide it, please contact admin.";
                if (video.Status != VideoStatus.Missing)
                {
                    video.SourceStatus = VideoStatus.Missing;
                    if (null != discordService)
                    {
                        await discordService.SendSkippedMessage(video);
                    }
                }
                video.Status = VideoStatus.Missing;
                _logger.LogInformation("Source removed and not archived, change video status to {status}", Enum.GetName(typeof(VideoStatus), video.Status));
            }

            if (video.Status >= VideoStatus.Archived && video.Status < VideoStatus.Expired)
            {
                video.Status = VideoStatus.Missing;
                video.Note = $"Video missing because archived not found.";
                _logger.LogInformation("Can not found archived, change video status to {status}", Enum.GetName(typeof(VideoStatus), video.Status));
            }
        }
        else if (video.Status < VideoStatus.Archived || video.Status >= VideoStatus.Expired)
        {
            video.Status = VideoStatus.Archived;
            video.Note = null;
            _logger.LogInformation("Correct video status to {status} because archived is exists.", Enum.GetName(typeof(VideoStatus), video.Status));
        }

        switch (videoData.Availability)
        {
            // Member only
            case "subscriber_only":
                if ((video.Channel?.UseCookiesFile) == true)
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
                    video.Note = $"Video source is Edited because it has been restored from rejection or deletion.";
                    if (video.SourceStatus != VideoStatus.Edited)
                    {
                        video.SourceStatus = VideoStatus.Edited;
                        if (null != discordService)
                        {
                            await discordService.SendDeletedMessage(video);
                        }
                    }
                    video.SourceStatus = VideoStatus.Edited;
                    _logger.LogInformation("Video source is {status} because it has been restored from rejection or deletion.", Enum.GetName(typeof(VideoStatus), video.SourceStatus));
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
                    video.Note = $"Video is Skipped because it is detected access required or copyright notice.";
                    // First detected
                    if (video.Status != VideoStatus.Skipped
                        && null != discordService)
                    {
                        await discordService.SendSkippedMessage(video);
                    }
                    video.Status = VideoStatus.Skipped;
                    _logger.LogInformation("Video is {status} because it is detected access required or copyright notice.", Enum.GetName(typeof(VideoStatus), video.Status));
                }
                // First detected
                else if (video.SourceStatus != VideoStatus.Reject)
                {
                    video.SourceStatus = VideoStatus.Reject;
                    video.Note = $"Video source is detected access required or copyright notice.";
                    if (null != discordService)
                    {
                        await discordService.SendDeletedMessage(video);
                    }
                }

                video.SourceStatus = VideoStatus.Reject;
                _logger.LogInformation("Video source is {status} because it is detected access required or copyright notice.", Enum.GetName(typeof(VideoStatus), video.SourceStatus));

                break;
        }

        if (video.Status == VideoStatus.WaitingToRecord)
        {
            await _ytarchiveService.InitJobAsync(url: video.id,
                                                 video: video,
                                                 useCookiesFile: video.Channel?.UseCookiesFile ?? false,
                                                 cancellation: cancellation);

            video.Status = VideoStatus.Recording;
            _logger.LogInformation("{videoId} is now lived! Start recording.", video.id);
            if (null != discordService)
            {
                await discordService.SendStartRecordingMessage(video);
            }
        }

        if (video.Status < 0)
            _logger.LogError("Video {videoId} has a Unknown status!", video.id);

        _videoRepository.AddOrUpdate(video);
        _unitOfWork_Public.Commit();
    }

    public override async Task UpdateChannelDataAsync(Channel channel, CancellationToken cancellation)
    {
        var avatarBlobUrl = channel.Avatar;
        var bannerBlobUrl = channel.Banner;
        var info = await GetChannelInfoByYtdlpAsync(channel.id, cancellation);
        if (null == info)
        {
            _logger.LogWarning("Failed to get channel info for {channelId}", channel.id);
            return;
        }

        var thumbnails = info.Thumbnails.OrderByDescending(p => p.Preference).ToList();
        var avatarUrl = thumbnails.FirstOrDefault()?.Url;
        if (!string.IsNullOrEmpty(avatarUrl))
        {
            avatarBlobUrl = await DownloadImageAndUploadToBlobStorage(avatarUrl, $"avatar/{channel.id}", cancellation);
        }

        var bannerUrl = thumbnails.Skip(1).FirstOrDefault()?.Url;
        if (!string.IsNullOrEmpty(bannerUrl))
        {
            bannerBlobUrl = await DownloadImageAndUploadToBlobStorage(bannerUrl, $"banner/{channel.id}", cancellation);
        }

        _unitOfWork_Public.Context.Entry(channel).Reload();
        channel = _channelRepository.LoadRelatedData(channel);
        channel.ChannelName = info.Uploader;
        channel.Avatar = avatarBlobUrl?.Replace("avatar/", "");
        channel.Banner = bannerBlobUrl?.Replace("banner/", "");
        _channelRepository.Update(channel);
        _unitOfWork_Public.Commit();
    }
}
