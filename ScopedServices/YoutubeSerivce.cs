using CodeHollow.FeedReader;
using LivestreamRecorderService.DB.Enum;
using LivestreamRecorderService.DB.Interfaces;
using LivestreamRecorderService.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Models;
using Serilog.Context;
using System.Configuration;
using YoutubeDLSharp.Options;
using File = LivestreamRecorderService.DB.Models.File;

namespace LivestreamRecorderService.ScopedServices;

public class YoutubeSerivce : PlatformService, IPlatformSerivce
{
    private readonly ILogger<YoutubeSerivce> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IVideoRepository _videoRepository;
    private readonly RSSService _rSSService;

    private string _ffmpegPath = "/usr/bin/ffmpeg";
    private string _ytdlPath = "/usr/bin/yt-dlp";

    public override string PlatformName => "Youtube";

    public override int Interval => 5 * 60;

    public YoutubeSerivce(
        ILogger<YoutubeSerivce> logger,
        IUnitOfWork unitOfWork,
        IVideoRepository videoRepository,
        IChannelRepository channelRepository,
        RSSService rSSService) : base(channelRepository)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _videoRepository = videoRepository;
        _rSSService = rSSService;
    }

    public static string GetRSSFeed(Channel channel)
        => $"https://www.youtube.com/feeds/videos.xml?channel_id={channel.id}";

    public override async Task UpdateVideosDataAsync(Channel channel, CancellationToken cancellation = default)
    {
        var feed = await _rSSService.ReadRSS(GetRSSFeed(channel), cancellation);

        if (null == feed)
        {
            _logger.LogError("Failed to get feed: {channel}", channel.ChannelName);
            return;
        }

        _rSSService.UpdateChannelName(channel, feed);

        foreach (var item in feed.Items)
        {
            await AddOrUpdateVideoAsync(channel, item, cancellation);
        }
        _unitOfWork.Commit();
    }

    private async Task<YtdlpVideoData> GetVideoInfoByYtdlpAsync(string videoId, CancellationToken cancellation = default)
    {
        if (!System.IO.File.Exists(_ytdlPath) || !System.IO.File.Exists(_ffmpegPath))
        {
            (string? YtdlPath, string? FFmpegPath) = YoutubeDL.WhereIs();
            _ytdlPath = YtdlPath ?? throw new ConfigurationErrorsException("Yt-dlp is missing.");
            _ffmpegPath = FFmpegPath ?? throw new ConfigurationErrorsException("FFmpeg is missing.");
            _logger.LogTrace("Use yt-dlp and ffmpeg executables at {ytdlp} and {ffmpeg}", _ytdlPath, _ffmpegPath);
        }
        var ytdl = new YoutubeDLSharp.YoutubeDL
        {
            YoutubeDLPath = _ytdlPath,
            FFmpegPath = _ffmpegPath
        };

        OptionSet optionSet = new();
        optionSet.AddCustomOption("--ignore-no-formats-error", true);

        var res = await ytdl.RunVideoDataFetch_Alt($"https://youtu.be/{videoId}", overrideOptions: optionSet, ct: cancellation);
        YtdlpVideoData videoData = res.Data;
        return videoData;
    }

    /// <summary>
    /// Update video status from RSS feed item.
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
            && video.Status >= VideoStatus.Recording)
        {
            _logger.LogTrace("Video {videoId} is skipped. It is {videoStatus}.", videoId, Enum.GetName(typeof(VideoStatus), video.Status));
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
                Title = item.Title,
                ChannelId = channel.id,
                Channel = channel,
                Timestamps = new Timestamps()
                {
                    PublishedAt = item.PublishingDate
                },
                Files = new List<File>()
            };
            _logger.LogInformation("Found a new Video {videoId} from {channelId}", videoId, channel.id);
            isNewVideo = true;
        }
        else
        {
            _videoRepository.LoadRelatedData(video);
        }

        YtdlpVideoData videoData = await GetVideoInfoByYtdlpAsync(videoId, cancellation);

        UpdateVideoData(video!, videoData);

        if (isNewVideo)
            _videoRepository.Add(video);
        else
            _videoRepository.Update(video);
    }

    private Video UpdateVideoData(Video video, YtdlpVideoData videoData)
    {
        video.Title = videoData.Title;
        video.Description = videoData.Description;
        video.Duration = videoData.Duration;

        switch (videoData.LiveStatus)
        {
            case "is_upcoming":
                video.Status = VideoStatus.Scheduled;
                video.Timestamps.ScheduledStartTime =
                    DateTimeOffset.FromUnixTimeSeconds(videoData.ReleaseTimestamp ?? 0).UtcDateTime;
                break;
            case "is_live":
                if (video.Status != VideoStatus.Recording)
                    video.Status = VideoStatus.WaitingToRecord;
                goto case "_live";
            case "post_live":
                // Livestream is finished but cannot download yet.
                if (video.Status != VideoStatus.Recording)
                    video.Status = VideoStatus.Pending;
                goto case "_live";
            case "was_live":
                if (video.Status != VideoStatus.Recording)
                    video.Status = VideoStatus.WaitingToDownload;
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
                video.Status = VideoStatus.WaitingToDownload;
                video.Timestamps.ActualStartTime ??=
                    videoData.ReleaseTimestamp.HasValue
                        ? DateTimeOffset.FromUnixTimeSeconds(videoData.ReleaseTimestamp.Value).UtcDateTime
                        : DateTime.ParseExact(videoData.UploadDate, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
                break;
            default:
                // Deleted
                if (string.IsNullOrEmpty(videoData.LiveStatus)
                   && videoData.Formats?.Count == 0)
                {
                    _logger.LogWarning("Failed to fetch video data, maybe it is deleted! {videoId}", video.id);
                }
                else
                {
                    // Unknown
                    goto case "not_live";
                }
                break;
        }

        switch (videoData.Availability)
        {
            case "public":
            case "unlisted":
                break;
            // Member only
            case "subscriber_only":
            // Copyright Notice
            case "needs_auth":
            default:
                video.Status = VideoStatus.Reject;
                break;
        }

        if (video.Status < 0)
            _logger.LogError("Video {videoId} has a Unknown status!", video.id);
        return video;
    }
}
