using LivestreamRecorder.DB.Enums;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
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
    private readonly IFc2LiveDLService _fC2LiveDLService;
    private readonly DiscordService? _discordService;

    public RecordService(ILogger<RecordWorker> logger,
        IJobService jobService,
        IYtarchiveService ytarchiveService,
        IYtdlpService ytdlpService,
        ITwitcastingRecorderService twitcastingRecorderService,
        IStreamlinkService streamlinkService,
        IFc2LiveDLService fC2LiveDLService,
        IOptions<DiscordOption> discordOptions,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _jobService = jobService;
        _ytarchiveService = ytarchiveService;
        _ytdlpService = ytdlpService;
        _twitcastingRecorderService = twitcastingRecorderService;
        _streamlinkService = streamlinkService;
        _fC2LiveDLService = fC2LiveDLService;
        if (discordOptions.Value.Enabled)
            _discordService = serviceProvider.GetRequiredService<DiscordService>();
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
                                             && DateTime.UtcNow.Subtract(p.Timestamps.ActualStartTime.Value).TotalMinutes >= 3)
                                 .ToList();

        if (videos.Count > 0)
            _logger.LogInformation("Get {count} videos recording/downloading: {videoIds}",
                videos.Count,
                string.Join(", ", videos.Select(p => p.id).ToList()));
        else
            _logger.LogTrace("No videos recording/downloading");

        foreach (Video video in videos)
        {
            if (await _jobService.IsJobFailedAsync(video, stoppingToken))
            {
                switch (video.Source)
                {
                    case "Youtube":
                        await videoService.UpdateVideoStatusAsync(video, VideoStatus.Pending);
                        _logger.LogWarning("{videoId} is failed. Set status to {status}", video.id, video.Status);
                        break;
                    default:
                        await videoService.UpdateVideoStatusAsync(video, VideoStatus.Error);
                        await videoService.UpdateVideoNoteAsync(video, "This recording FAILED! Please contact admin if you see this message.");
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
    /// <param name="channelService"></param>
    /// <param name="stoppingToken"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    public async Task CreateStartRecordJobAsync(VideoService videoService, ChannelService channelService, CancellationToken stoppingToken)
    {
        _logger.LogDebug("Getting videos to record");
        List<Video> videos = videoService.GetVideosByStatus(VideoStatus.WaitingToRecord);

        // Livestream will start recording immediately when detected goes live.
        // So in fact these cases will only be executed when HandledFailedJobsAsync() occured.
        if (videos.Count > 0)
            _logger.LogInformation("Get {count} videos to record: {videoIds}", videos.Count, string.Join(", ", videos.Select(p => p.id).ToList()));
        else
            _logger.LogTrace("No videos waiting to record");

        foreach (Video video in videos)
        {
            using IDisposable _ = LogContext.PushProperty("videoId", video.id);
            Channel? channel = await channelService.GetByChannelIdAndSourceAsync(video.ChannelId, video.Source);
            _logger.LogInformation("Start to create recording job: {videoId}", video.id);
            try
            {
                switch (video.Source)
                {
                    case "Youtube":
                        await _ytarchiveService.InitJobAsync(url: video.id,
                            video: video,
                            useCookiesFile: channel?.UseCookiesFile == true,
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
                            useCookiesFile: channel?.UseCookiesFile == true,
                            cancellation: stoppingToken);

                        break;

                    default:
                        _logger.LogError("Job deployment FAILED, Source not support: {source}", video.Source);
                        throw new NotSupportedException($"Source {video.Source} not supported");
                }

                string? filename = video.Filename;

                await videoService.UpdateVideoFilenameAsync(video, filename);
                await videoService.UpdateVideoStatusAsync(video, VideoStatus.Recording);

                _logger.LogInformation("Job deployed: {videoId} ", video.id);
                _logger.LogInformation("Start to record {videoId}", video.id);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Job deployment FAILED: {videoId}", video.id);
                await videoService.UpdateVideoStatusAsync(video, VideoStatus.Error);
                await videoService.UpdateVideoNoteAsync(video,
                    "Exception happened when starting recording job. Please contact admin if you see this message");
            }
        }
    }

    /// <summary>
    /// Create jobs to download videos.
    /// </summary>
    /// <param name="videoService"></param>
    /// <param name="channelService"></param>
    /// <param name="stoppingToken"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    public async Task CreateStartDownloadJobAsync(VideoService videoService, ChannelService channelService, CancellationToken stoppingToken)
    {
        _logger.LogDebug("Getting videos to download");
        List<Video> videos = videoService.GetVideosByStatus(VideoStatus.WaitingToDownload);
        if (videos.Count > 0)
            _logger.LogInformation("Get {count} videos to download: {videoIds}", videos.Count, string.Join(", ", videos.Select(p => p.id).ToList()));
        else
            _logger.LogTrace("No videos waiting to download");

        foreach (Video video in videos)
        {
            using IDisposable _ = LogContext.PushProperty("videoId", video.id);
            Channel? channel = await channelService.GetByChannelIdAndSourceAsync(video.ChannelId, video.Source);
            _logger.LogInformation("Start to create downloading job: {videoId}", video.id);
            try
            {
                switch (video.Source)
                {
                    case "Youtube":
                    {
                        string id = NameHelper.ChangeId.VideoId.PlatformType(video.id, "Youtube");
                        await _ytdlpService.InitJobAsync(url: $"https://youtu.be/{id}",
                            video: video,
                            useCookiesFile: channel?.UseCookiesFile == true,
                            cancellation: stoppingToken);
                    }

                        break;
                    case "Twitcasting":
                    {
                        string channelId = NameHelper.ChangeId.ChannelId.PlatformType(video.ChannelId, "Twitcasting");
                        string videoId = NameHelper.ChangeId.VideoId.PlatformType(video.id, "Twitcasting");
                        await _ytdlpService.InitJobAsync(url: $"https://twitcasting.tv/{channelId}/movie/{videoId}",
                            video: video,
                            useCookiesFile: false,
                            cancellation: stoppingToken);
                    }

                        break;
                    case "Twitch":
                    {
                        string id = NameHelper.ChangeId.VideoId.PlatformType(video.id, "Twitch");
                        await _ytdlpService.InitJobAsync(url: $"https://www.twitch.tv/videos/{id}",
                            video: video,
                            useCookiesFile: false,
                            cancellation: stoppingToken);
                    }

                        break;
                    case "FC2":
                    {
                        string id = NameHelper.ChangeId.VideoId.PlatformType(video.id, "FC2");
                        await _ytdlpService.InitJobAsync(url: $"https://video.fc2.com/content/{id}",
                            video: video,
                            useCookiesFile: channel?.UseCookiesFile == true,
                            cancellation: stoppingToken);
                    }

                        break;

                    default:
                        _logger.LogError("Job deployment FAILED, Source not support: {source}", video.Source);
                        throw new NotSupportedException($"Source {video.Source} not supported");
                }

                string? filename = video.Filename;

                await videoService.UpdateVideoFilenameAsync(video, filename);
                await videoService.UpdateVideoStatusAsync(video, VideoStatus.Downloading);

                _logger.LogInformation("Job deployed: {videoId} ", video.id);
                _logger.LogInformation("Start to download {videoId}", video.id);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Job deployment FAILED: {videoId}", video.id);
                await videoService.UpdateVideoStatusAsync(video, VideoStatus.Error);
                await videoService.UpdateVideoNoteAsync(video,
                    "Exception happened when starting downloading job. Please contact admin if you see this message");
            }
        }
    }


    /// <summary>
    /// Check recordings and downloading status and return finished videos
    /// </summary>
    /// <param name="videoService"></param>
    /// <param name="cancellation"></param>
    /// <returns>Videos that finish uploading.</returns>
    public async Task<List<Video>> MonitorRecordingDownloadingVideosAsync(VideoService videoService, CancellationToken cancellation = default)
    {
        var result = new List<Video>();
        IEnumerable<Video> videos = videoService.GetVideosByStatus(VideoStatus.Recording)
                                                .Concat(videoService.GetVideosByStatus(VideoStatus.Downloading));

        foreach (Video video in videos)
        {
            using IDisposable _ = LogContext.PushProperty("videoId", video.id);

            bool succeed = await _jobService.IsJobSucceededAsync(video, cancellation);
            if (!succeed) continue;

            _logger.LogInformation("Video recording finish {videoId}", video.id);
            result.Add(video);
        }

        return result;
    }

    public async Task ProcessFinishedVideoAsync(VideoService videoService,
                                                ChannelService channelService,
                                                Video video,
                                                CancellationToken stoppingToken = default)
    {
        using IDisposable __ = LogContext.PushProperty("videoId", video.id);

        await channelService.UpdateChannelLatestVideoAsync(video);

        try
        {
            await _jobService.RemoveCompletedJobsAsync(video, stoppingToken);

            await videoService.UpdateVideoStatusAsync(video, VideoStatus.Archived);
            _logger.LogInformation("Video {videoId} is successfully uploaded to Storage.", video.id);
            await videoService.UpdateVideoArchivedTimeAsync(video);

            if (_discordService != null)
            {
                await _discordService.SendArchivedMessageAsync(
                    video,
                    await channelService.GetByChannelIdAndSourceAsync(video.ChannelId, video.Source));
            }
        }
        catch (Exception e)
        {
            await videoService.UpdateVideoStatusAsync(video, VideoStatus.Error);
            await videoService.UpdateVideoNoteAsync(video, "This recording is FAILED! Please contact admin if you see this message.");
            _logger.LogError(e, "Recording FAILED: {videoId}", video.id);
            return;
        }
    }
}
