using LivestreamRecorder.DB.Enums;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Interfaces.Job;
using LivestreamRecorderService.Interfaces.Job.Downloader;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.ScopedServices;
using LivestreamRecorderService.Workers;
using Microsoft.Extensions.Options;
using Serilog.Context;

namespace LivestreamRecorderService.SingletonServices;

public class RecordService
{
    private readonly ILogger<RecordWorker> _logger;
    private readonly IJobService _jobService;
    private readonly IYtarchiveService _ytarchiveService;
    private readonly IYtdlpService _ytdlpService;
    private readonly ITwitcastingRecorderService _twitcastingRecorderService;
    private readonly IStreamlinkService _streamlinkService;
    private readonly IFC2LiveDLService _fC2LiveDLService;
    private readonly DiscordService _discordService;

    public RecordService(
        ILogger<RecordWorker> logger,
        IJobService jobService,
        IYtarchiveService ytarchiveService,
        IYtdlpService ytdlpService,
        ITwitcastingRecorderService twitcastingRecorderService,
        IStreamlinkService streamlinkService,
        IFC2LiveDLService fC2LiveDLService,
        DiscordService discordService,
        IOptions<AzureOption> options)
    {
        _logger = logger;
        _jobService = jobService;
        _ytarchiveService = ytarchiveService;
        _ytdlpService = ytdlpService;
        _twitcastingRecorderService = twitcastingRecorderService;
        _streamlinkService = streamlinkService;
        _fC2LiveDLService = fC2LiveDLService;
        _discordService = discordService;
    }

    /// <summary>
    /// Handled failed jobs.
    /// </summary>
    /// <param name="videoService"></param>
    /// <param name="stoppingToken"></param>
    /// <returns></returns>
    public async Task HandledFailedJobsAsync(VideoService videoService, CancellationToken stoppingToken)
    {
        var videos = videoService.GetVideosByStatus(VideoStatus.Recording)
             .Concat(videoService.GetVideosByStatus(VideoStatus.Downloading))
             // Only check videos that started recording/download more than 3 minutes ago
             // to avoid checking videos that are not finished deployment yet.
             .Where(p => null != p.Timestamps.ActualStartTime
                         && DateTime.Now.Subtract(p.Timestamps.ActualStartTime.Value).TotalMinutes >= 3)
             .ToList();

        if (videos.Count > 0)
            _logger.LogInformation("Get {count} videos recording/downloading: {videoIds}", videos.Count, string.Join(", ", videos.Select(p => p.id).ToList()));
        else
            _logger.LogTrace("No videos recording/downloading");

        foreach (var video in videos)
        {
            if (await _jobService.IsJobFailedAsync(video, stoppingToken))
            {
                switch (video.Source)
                {
                    case "Youtube":
                        videoService.UpdateVideoStatus(video, VideoStatus.Scheduled);
                        _logger.LogWarning("{videoId} is failed. Set status to Scheduled", video.id);
                        break;
                    default:
                        videoService.UpdateVideoStatus(video, VideoStatus.Error);
                        videoService.UpdateVideoNote(video, $"This recording FAILED! Please contact admin if you see this message.");
                        _logger.LogWarning("{videoId} is failed.", video.id);
                        break;
                }
            }
        }
    }

    /// <summary>
    /// Create jobs to record videos.
    /// </summary>
    /// <param name="videoService"></param>
    /// <param name="stoppingToken"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    public async Task CreateStartRecordJobAsync(VideoService videoService, CancellationToken stoppingToken)
    {
        _logger.LogDebug("Getting videos to record");
        var videos = videoService.GetVideosByStatus(VideoStatus.WaitingToRecord);

        // Livestream will start recording immediately when detected goes live.
        // So in fact these cases will only be executed when HandledFailedACIsAsync() occured.
        if (videos.Count > 0)
            _logger.LogInformation("Get {count} videos to record: {videoIds}", videos.Count, string.Join(", ", videos.Select(p => p.id).ToList()));
        else
            _logger.LogTrace("No videos waiting to record");

        foreach (var video in videos)
        {
            using var _ = LogContext.PushProperty("videoId", video.id);
            _logger.LogInformation("Start to create ACI: {videoId}", video.id);
            try
            {
                switch (video.Source)
                {
                    case "Youtube":
                        await _ytarchiveService.InitJobAsync(url: video.id,
                                                             video: video,
                                                             useCookiesFile: video.Channel?.UseCookiesFile == true,
                                                             cancellation: stoppingToken);
                        break;
                    case "Twitcasting":
                        await _twitcastingRecorderService.InitJobAsync(url: video.id,
                                                                       video: video,
                                                                       useCookiesFile: false,
                                                                       cancellation: stoppingToken);
                        break;
                    case "Twitch":
                        await _streamlinkService.InitJobAsync(url: video.id,
                                                              video: video,
                                                              useCookiesFile: false,
                                                              cancellation: stoppingToken);
                        break;
                    case "FC2":
                        await _fC2LiveDLService.InitJobAsync(url: video.id,
                                                             video: video,
                                                             useCookiesFile: video.Channel?.UseCookiesFile == true,
                                                             cancellation: stoppingToken);
                        break;

                    default:
                        _logger.LogError("ACI deployment FAILED, Source not support: {source}", video.Source);
                        throw new NotSupportedException($"Source {video.Source} not supported");
                }
                videoService.UpdateVideoStatus(video, VideoStatus.Recording);

                _logger.LogInformation("ACI deployed: {videoId} ", video.id);
                _logger.LogInformation("Start to record {videoId}", video.id);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "ACI deployment FAILED: {videoId}", video.id);
                videoService.UpdateVideoStatus(video, VideoStatus.Error);
                videoService.UpdateVideoNote(video, "Exception happened when starting recording ACI. Please contact admin if you see this message");
            }
        }
    }

    /// <summary>
    /// Create jobs to download videos.
    /// </summary>
    /// <param name="videoService"></param>
    /// <param name="stoppingToken"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    public async Task CreateStartDownloadJobAsync(VideoService videoService, CancellationToken stoppingToken)
    {
        _logger.LogDebug("Getting videos to download");
        var videos = videoService.GetVideosByStatus(VideoStatus.WaitingToDownload);
        if (videos.Count > 0)
            _logger.LogInformation("Get {count} videos to download: {videoIds}", videos.Count, string.Join(", ", videos.Select(p => p.id).ToList()));
        else
            _logger.LogTrace("No videos waiting to download", videos.Count);

        foreach (var video in videos)
        {
            using var _ = LogContext.PushProperty("videoId", video.id);
            _logger.LogInformation("Start to create ACI: {videoId}", video.id);
            try
            {
                switch (video.Source)
                {
                    case "Youtube":
                        await _ytdlpService.InitJobAsync(
                            url: $"https://youtu.be/{video.id}",
                            video: video,
                            useCookiesFile: video.Channel?.UseCookiesFile == true,
                            cancellation: stoppingToken);
                        break;
                    case "Twitcasting":
                        await _ytdlpService.InitJobAsync(
                            url: $"https://twitcasting.tv/{video.ChannelId}/movie/{video.id}",
                            video: video,
                            useCookiesFile: false,
                            cancellation: stoppingToken);
                        break;
                    case "Twitch":
                        var id = video.id.TrimStart('v');
                        await _ytdlpService.InitJobAsync(
                            url: $"https://www.twitch.tv/videos/{id}",
                            video: video,
                            useCookiesFile: false,
                            cancellation: stoppingToken);
                        break;
                    case "FC2":
                        await _ytdlpService.InitJobAsync(
                            url: $"https://video.fc2.com/content/{video.id}",
                            video: video,
                            useCookiesFile: video.Channel?.UseCookiesFile == true,
                            cancellation: stoppingToken);
                        break;

                    default:
                        _logger.LogError("ACI deployment FAILED, Source not support: {source}", video.Source);
                        throw new NotSupportedException($"Source {video.Source} not supported");
                }
                videoService.UpdateVideoStatus(video, VideoStatus.Downloading);
                _logger.LogInformation("ACI deployed: {videoId} ", video.id);
                _logger.LogInformation("Start to download {videoId}", video.id);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "ACI deployment FAILED: {videoId}", video.id);
                videoService.UpdateVideoStatus(video, VideoStatus.Error);
                videoService.UpdateVideoNote(video, "Exception happened when starting downloading ACI. Please contact admin if you see this message");
            }
        }
    }


    /// <summary>
    /// Check recordings status and return uploaded videos
    /// </summary>
    /// <param name="videoService"></param>
    /// <param name="cancellation"></param>
    /// <returns>Videos that finish uploading.</returns>
    public async Task<List<Video>> MonitorRecordingVideosAsync(VideoService videoService, CancellationToken cancellation = default)
    {
        var result = new List<Video>();
        var videos = videoService.GetVideosByStatus(VideoStatus.Recording)
             .Concat(videoService.GetVideosByStatus(VideoStatus.Downloading));
        foreach (var video in videos)
        {
            using var _ = LogContext.PushProperty("videoId", video.id);

            var successed = await _jobService.IsJobSucceededAsync(video, cancellation);
            if (successed)
            {
                _logger.LogInformation("Video recording finish {videoId}", video.id);
                result.Add(video);
            }
        }
        return result;
    }

    /// <summary>
    /// Check recordings status and return finished videos
    /// </summary>
    /// <param name="videoService"></param>
    /// <returns>Videos that finish recording.</returns>
    public async Task<List<Video>> MonitorUploadedVideosAsync(VideoService videoService, CancellationToken cancellation = default)
    {
        var result = new List<Video>();
        var videos = videoService.GetVideosByStatus(VideoStatus.Uploading);
        foreach (var video in videos)
        {
            using var _ = LogContext.PushProperty("videoId", video.id);

            var successed = await _jobService.IsJobSucceededAsync(video, cancellation);
            if (successed)
            {
                _logger.LogInformation("Video recording finish {videoId}", video.id);
                result.Add(video);
            }
        }
        return result;
    }

    public async Task PcocessFinishedVideo(VideoService videoService, ChannelService channelService, Video video, CancellationToken stoppingToken = default)
    {
        using var __ = LogContext.PushProperty("videoId", video.id);

        try
        {
            await _jobService.RemoveCompletedJobsAsync(video, stoppingToken);
        }
        catch (Exception)
        {
            videoService.UpdateVideoStatus(video, VideoStatus.Error);
            videoService.UpdateVideoNote(video, $"This recording is FAILED! Please contact admin if you see this message.");
        }

        channelService.UpdateChannelLatestVideo(video);

        videoService.UpdateVideoArchivedTime(video);

        // Fire and forget
        _ = videoService.TransferVideoFromSharedVolumeToStorageAsync(video, stoppingToken).ConfigureAwait(false);
    }

    public async Task ProcessUploadedVideo(VideoService videoService, Video video, CancellationToken stoppingToken = default)
    {
        using var _ = LogContext.PushProperty("videoId", video.id);

        try
        {
            await _jobService.RemoveCompletedJobsAsync(video, stoppingToken);
        }
        catch (Exception)
        {
            videoService.UpdateVideoStatus(video, VideoStatus.Error);
            videoService.UpdateVideoNote(video, $"This recording is FAILED! Please contact admin if you see this message.");
        }

        await _discordService.SendArchivedMessage(video);
        _logger.LogInformation("Video {videoId} is uploaded to Storage.", video.id);
        videoService.UpdateVideoStatus(video, VideoStatus.Archived);
    }

}
