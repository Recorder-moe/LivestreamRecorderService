using CodeHollow.FeedReader;
using LivestreamRecorderService.DB.Enum;
using LivestreamRecorderService.DB.Interfaces;
using LivestreamRecorderService.DB.Models;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Models;
using Serilog.Context;

namespace LivestreamRecorderService.ScopedServices;

public class YoutubeSerivce : PlatformService, IPlatformSerivce
{
    private readonly ILogger<YoutubeSerivce> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IVideoRepository _videoRepository;
    private readonly IChannelRepository _channelRepository;
    private readonly RSSService _rSSService;
    private readonly IABSService _aBSService;

    public override string PlatformName => "Youtube";

    public override int Interval => 5 * 60;

    public YoutubeSerivce(
        ILogger<YoutubeSerivce> logger,
        IUnitOfWork unitOfWork,
        IVideoRepository videoRepository,
        IChannelRepository channelRepository,
        RSSService rSSService,
        IABSService aBSService,
        IHttpClientFactory httpClientFactory) : base(channelRepository, aBSService, httpClientFactory)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _videoRepository = videoRepository;
        _channelRepository = channelRepository;
        _rSSService = rSSService;
        _aBSService = aBSService;
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

        _logger.LogDebug("Get {count} videos for channel {channelId}", feed.Items.Count, channel.id);
        foreach (var item in feed.Items)
        {
            await AddOrUpdateVideoAsync(channel, item, cancellation);
        }
        _unitOfWork.Commit();
    }

    private Task<YtdlpVideoData> GetChannelInfoByYtdlpAsync(string ChannelId, CancellationToken cancellation = default)
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

        bool isNewVideo = false;
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
            isNewVideo = true;
        }
        else
        {
            _videoRepository.LoadRelatedData(video);
        }

        await UpdateVideoDataWithoutCommitAsync(video!, cancellation);

        if (isNewVideo)
            _videoRepository.Add(video);
        else
            _videoRepository.Update(video);
    }

    public override async Task UpdateVideoDataAsync(Video video, CancellationToken cancellation = default)
    {
        await UpdateVideoDataWithoutCommitAsync(video, cancellation);
        _videoRepository.Update(video);
        _unitOfWork.Commit();
    }

    /// <summary>
    /// Update video data.
    /// </summary>
    /// <remarks>!!! Updates will not save to DB !!! Must call SaveChanges yourself !!!</remarks>
    /// <param name="video"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    public async Task UpdateVideoDataWithoutCommitAsync(Video video, CancellationToken cancellation = default)
    {
        YtdlpVideoData videoData = await GetVideoInfoByYtdlpAsync($"https://youtu.be/{video.id}", cancellation);

        switch (videoData.LiveStatus)
        {
            case "is_upcoming":
                // New stream published
                if (video.Status == VideoStatus.Unknown)
                    video.Thumbnail = await DownloadThumbnailAsync(videoData.Thumbnail, video.id, cancellation);

                video.Status = VideoStatus.Scheduled;
                video.Timestamps.ScheduledStartTime =
                    DateTimeOffset.FromUnixTimeSeconds(videoData.ReleaseTimestamp ?? 0).UtcDateTime;
                break;
            case "is_live":
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
                goto case "_live";
            case "was_live":
                switch (video.Status)
                {
                    // Old unarchived streams.
                    // Will fall in here when adding a new channel.
                    case VideoStatus.Unknown:
                        video.Status = VideoStatus.Expired;
                        _logger.LogInformation("Change video {videoId} status to {videoStatus}", video.id, Enum.GetName(typeof(VideoStatus), video.Status));
                        video.Thumbnail = await DownloadThumbnailAsync(videoData.Thumbnail, video.id, cancellation);
                        break;
                    // Should record these streams but not recorded.
                    // Download them.
                    case VideoStatus.Scheduled:
                    case VideoStatus.Pending:
                    case VideoStatus.WaitingToRecord:
                        video.Status = VideoStatus.WaitingToDownload;
                        video.Thumbnail = await DownloadThumbnailAsync(videoData.Thumbnail, video.id, cancellation);
                        _logger.LogInformation("Change video {videoId} status to {videoStatus}", video.id, Enum.GetName(typeof(VideoStatus), video.Status));
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
                if (video.Status == VideoStatus.Unknown)
                {
                    video.Status = VideoStatus.Reject;
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
                    video.SourceStatus = VideoStatus.Deleted;
                    _logger.LogInformation("Failed to fetch video data, maybe it is deleted! {videoId}", video.id);
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

        if (await _aBSService.GetBlobByVideo(video)
                             .ExistsAsync(cancellation))
        {
            video.Status = VideoStatus.Archived;
        }
        else if (video.SourceStatus == VideoStatus.Deleted
                 && video.Status < VideoStatus.Uploading)
        {
            video.Status = VideoStatus.Missing;
            _logger.LogInformation("Source removed and not archived, change video status to {status}", Enum.GetName(typeof(VideoStatus), VideoStatus.Missing));
        }
        else if (video.Status == VideoStatus.Archived)
        {
            video.Status = VideoStatus.Expired;
            _logger.LogInformation("Can not found archived, change video status to {status}", Enum.GetName(typeof(VideoStatus), VideoStatus.Expired));
        }

        switch (videoData.Availability)
        {
            case "public":
            case "unlisted":
                video.SourceStatus = VideoStatus.Exist;
                break;
            // Member only
            case "subscriber_only":
            // Copyright Notice
            case "needs_auth":
                video.Status = VideoStatus.Reject;
                video.SourceStatus = VideoStatus.Reject;
                _logger.LogInformation("Video is detected member_only or needs_auth, change video status to {status}", Enum.GetName(typeof(VideoStatus), VideoStatus.Reject));
                break;
        }

        if (video.Status < 0)
            _logger.LogError("Video {videoId} has a Unknown status!", video.id);
    }

    internal async Task UpdateChannelData(Channel channel, CancellationToken cancellation)
    {
        var avatarBlobUrl = channel.Avatar;
        var bannerBlobUrl = channel.Banner;
        var info = await GetChannelInfoByYtdlpAsync(channel.id, cancellation);

        var thumbnails = info.Thumbnails.OrderByDescending(p => p.Preference).ToList();
        var avatarUrl = thumbnails.FirstOrDefault()?.Url;
        if (!string.IsNullOrEmpty(avatarUrl))
        {
            avatarBlobUrl = await DownloadImageAndUploadToBlobStorage(avatarUrl, $"avatar/{channel.id}", cancellation);
        }

        var bannerUrl = thumbnails.Skip(1).FirstOrDefault()?.Url;
        if (!string.IsNullOrEmpty(bannerUrl))
        {
            bannerBlobUrl = (await DownloadImageAndUploadToBlobStorage(bannerUrl, $"banner/{channel.id}", cancellation));
        }

        _unitOfWork.Context.Entry(channel).Reload();
        channel = _channelRepository.LoadRelatedData(channel);
        channel.ChannelName = info.Uploader;
        channel.Avatar = avatarBlobUrl?.Replace("avatar/", "");
        channel.Banner = bannerBlobUrl?.Replace("banner/", "");
        _channelRepository.Update(channel);
        _unitOfWork.Commit();
    }
}
