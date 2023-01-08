using Azure.Storage.Files.Shares.Models;
using CodeHollow.FeedReader;
using LivestreamRecorderService.DB.Enum;
using LivestreamRecorderService.DB.Interfaces;
using LivestreamRecorderService.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Models;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.SingletonServices;
using Microsoft.Extensions.Options;
using Serilog.Context;
using System.Configuration;
using System.Web;
using File = LivestreamRecorderService.DB.Models.File;

namespace LivestreamRecorderService.ScopedServices;

public class VideoService
{
    private string _ffmpegPath = "/usr/bin/ffmpeg";
    private string _ytdlPath = "/usr/bin/yt-dlp";
    private readonly ILogger<VideoService> _logger;
    private readonly IVideoRepository _videoRepository;
    private readonly IFileRepository _fileRepository;
    private readonly IHttpClientFactory _httpFactory;

    public VideoService(
        ILogger<VideoService> logger,
        IVideoRepository videoRepository,
        IFileRepository fileRepository,
        IHttpClientFactory httpFactory,
        IOptions<AzureOption> options)
    {
        _logger = logger;
        _videoRepository = videoRepository;
        _fileRepository = fileRepository;
        _httpFactory = httpFactory;
    }

    public List<Video> GetVideosByStatus(VideoStatus status)
        => _videoRepository.Where(p => p.Status == status).ToList();

    public async Task UpdateVideoStatus(Video video, VideoStatus status)
    {
        var entity = await _videoRepository.GetByIdAsync(video.id);
        entity.Status = status;
        await _videoRepository.UpdateAsync(entity);
        await _videoRepository.SaveChangesAsync();
        _logger.LogDebug("Update Video Status to {videostatus}", status);
    }

    public Task ACIDeployedAsync(Video video)
        => UpdateVideoStatus(video, VideoStatus.Recording);

    public async Task AddFilesToVideoAsync(Video video, List<ShareFileItem> sharefileItems)
    {
        video = await _videoRepository.GetByIdAsync(video.id);
        _videoRepository.LoadRelatedData(video);
        var files = AFSService.ConvertFileShareItemsToFilesEntities(video, sharefileItems);

        // Remove files if already exists.
        foreach (var file in files)
        {
            if (await _fileRepository.ExistsAsync(file.id))
                await _fileRepository.DeleteAsync(await _fileRepository.GetByIdAsync(file.id));
        }
        await _fileRepository.SaveChangesAsync();

        video.Files = files;
        video.ArchivedTime = DateTime.UtcNow;
        await _videoRepository.UpdateAsync(video);
        await _videoRepository.SaveChangesAsync();
    }

    public async Task TransferVideoToBlobStorageAsync(Video video)
    {
        var oldStatus = video.Status;
        await UpdateVideoStatus(video, VideoStatus.Uploading);
        await _videoRepository.SaveChangesAsync();

        try
        {
            _logger.LogInformation("Call Azure Function to transfer video to blob storage: {videoId}", video.id);
            using var client = _httpFactory.CreateClient();
            var response = await client.PostAsync("AzureFileShares2BlobContainers?videoId=" + HttpUtility.UrlEncode(video.id), null);
            response.EnsureSuccessStatusCode();

            await UpdateVideoStatus(video, VideoStatus.Archived);
        }
        catch (Exception e)
        {
            await UpdateVideoStatus(video, oldStatus);
            _logger.LogError("Exception happened when calling Azure Function to transfer files to blob storage: {videoId}, {error}, {message}", video.id, e, e.Message);
        }
        await _videoRepository.SaveChangesAsync();
    }

    /// <summary>
    /// Update video status from RSS feed item.
    /// </summary>
    /// <remarks>!!! Updates will not save to DB !!! Must call SaveChanges yourself !!!</remarks>
    /// <param name="channel"></param>
    /// <param name="item"></param>
    /// <returns></returns>
    public async Task UpdateVideosDataAsync(Channel channel, FeedItem item)
    {
        var videoId = item.Id.Split(':').Last();
        using var _ = LogContext.PushProperty("videoId", videoId);
        var video = (await _videoRepository.ExistsAsync(videoId))
                    ? await _videoRepository.GetByIdAsync(videoId)
                    : null;

        // Don't need to track anymore.
        if (null != video
            && video.Status >= VideoStatus.Recording)
        {
            _logger.LogTrace("Video {videoId} is skipped. It is {videoStatus}.", videoId, Enum.GetName(typeof(VideoService), video.Status));
            return;
        }

        bool isNewVideo = false;
        if (null != video)
        {
            _videoRepository.LoadRelatedData(video);
        }
        else
        {
            video = new Video()
            {
                id = videoId,
                Source = "Youtube",
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

        // Get video info by yt-dlp
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

        var res = await ytdl.RunVideoDataFetch_Alt(videoId);
        YtdlpVideoData videoData = res.Data;

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
                video.Timestamps.ActualStartTime =
                    DateTimeOffset.FromUnixTimeSeconds(videoData.ReleaseTimestamp ?? 0).UtcDateTime;
                break;
            default:
                // Deleted
                if (string.IsNullOrEmpty(videoData.LiveStatus)
                   && videoData.Formats?.Count == 0)
                {
                    _logger.LogWarning("Failed to fetch video data, maybe it is deleted! {videoId}", videoId);
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
            _logger.LogError("Video {videoId} has a Unknown status!", videoId);

        if (isNewVideo)
            await _videoRepository.AddAsync(video);
        else
            await _videoRepository.UpdateAsync(video);
    }

    public async Task SaveIfVideoContextHasChangesAsync()
    {
        if (_videoRepository.HasChanged)
            await _videoRepository.SaveChangesAsync();
    }
}
