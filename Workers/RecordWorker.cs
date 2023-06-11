using LivestreamRecorder.DB.Enum;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Interfaces.Job;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.ScopedServices;
using Microsoft.Extensions.Options;
using Serilog.Context;
using FileInfo = LivestreamRecorderService.Models.FileInfo;

namespace LivestreamRecorderService.Workers;

public class RecordWorker : BackgroundService
{
    private readonly ILogger<RecordWorker> _logger;
    private readonly IJobService _jobService;
    private readonly IYtarchiveService _ytarchiveService;
    private readonly IYtdlpService _ytdlpService;
    private readonly ITwitcastingRecorderService _twitcastingRecorderService;
    private readonly IStreamlinkService _streamlinkService;
    private readonly IFC2LiveDLService _fC2LiveDLService;
    private readonly ISharedVolumeService _sharedVolumeService;
    private readonly IServiceProvider _serviceProvider;

    public RecordWorker(
        ILogger<RecordWorker> logger,
        IJobService jobService,
        IYtarchiveService ytarchiveService,
        IYtdlpService ytdlpService,
        ITwitcastingRecorderService twitcastingRecorderService,
        IStreamlinkService streamlinkService,
        IFC2LiveDLService fC2LiveDLService,
        ISharedVolumeService sharedVolumeService,
        IOptions<AzureOption> options,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _jobService = jobService;
        _ytarchiveService = ytarchiveService;
        _ytdlpService = ytdlpService;
        _twitcastingRecorderService = twitcastingRecorderService;
        _streamlinkService = streamlinkService;
        _fC2LiveDLService = fC2LiveDLService;
        _sharedVolumeService = sharedVolumeService;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var __ = LogContext.PushProperty("Worker", nameof(RecordWorker));

        _logger.LogInformation("{Worker} will sleep 30 seconds to wait for {WorkerToWait} to start.", nameof(RecordWorker), nameof(MonitorWorker));
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        _logger.LogTrace("{Worker} starts...", nameof(RecordWorker));
        while (!stoppingToken.IsCancellationRequested)
        {
            using var ____ = LogContext.PushProperty("WorkerRunId", $"{nameof(RecordWorker)}_{DateTime.Now:yyyyMMddHHmmssfff}");
            #region DI
            using (var scope = _serviceProvider.CreateScope())
            {
                var videoService = scope.ServiceProvider.GetRequiredService<VideoService>();
                var channelService = scope.ServiceProvider.GetRequiredService<ChannelService>();
                #endregion

                await HandledFailedJobsAsync(videoService, stoppingToken);

                await CreateStartRecordJobAsync(videoService, stoppingToken);
                await CreateStartDownloadJobAsync(videoService, stoppingToken);

                videoService.RollbackVideosStatusStuckAtUploading();

                var finished = await MonitorRecordingVideosAsync(videoService, stoppingToken);
                List<Task> tasks = new();
                foreach (var kvp in finished)
                {
                    var (video, file) = (kvp.Key, kvp.Value);
                    using var ___ = LogContext.PushProperty("videoId", video.id);

                    try
                    {
                        await _jobService.RemoveCompletedJobsAsync(video, stoppingToken);
                    }
                    catch (Exception)
                    {
                        videoService.UpdateVideoStatus(video, VideoStatus.Error);
                        videoService.UpdateVideoNote(video, $"This recording FAILED! Please contact admin if you see this message.");
                    }

                    channelService.UpdateChannelLatestVideo(video);

                    videoService.AddFilePropertiesToVideo(video, file);

                    tasks.Add(videoService.TransferVideoFromPVToStorageAsync(video, file, stoppingToken));

                    // Avoid concurrency requests
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                await Task.WhenAll(tasks);
            }

            _logger.LogTrace("{Worker} ends. Sleep 5 minutes.", nameof(RecordWorker));
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private async Task HandledFailedJobsAsync(VideoService videoService, CancellationToken stoppingToken)
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
    /// CreateACIStartRecord
    /// </summary>
    /// <param name="videoService"></param>
    /// <param name="stoppingToken"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    private async Task CreateStartRecordJobAsync(VideoService videoService, CancellationToken stoppingToken)
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
            switch (video.Source)
            {
                case "Youtube":
                    await _ytarchiveService.InitJobAsync(url: video.id,
                                                                  channelId: video.ChannelId,
                                                                  useCookiesFile: video.Channel?.UseCookiesFile == true,
                                                                  cancellation: stoppingToken);
                    break;
                case "Twitcasting":
                    await _twitcastingRecorderService.InitJobAsync(url: video.id,
                                                                            channelId: video.ChannelId,
                                                                            useCookiesFile: false,
                                                                            cancellation: stoppingToken);
                    break;
                case "Twitch":
                    await _streamlinkService.InitJobAsync(url: video.id,
                                                                   channelId: video.ChannelId,
                                                                   useCookiesFile: false,
                                                                   cancellation: stoppingToken);
                    break;
                case "FC2":
                    await _fC2LiveDLService.InitJobAsync(url: video.id,
                                                                  channelId: video.ChannelId,
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
    }

    private async Task CreateStartDownloadJobAsync(VideoService videoService, CancellationToken stoppingToken)
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
            switch (video.Source)
            {
                case "Youtube":
                    await _ytdlpService.InitJobAsync(
                        url: $"https://youtu.be/{video.id}",
                        channelId: video.ChannelId,
                        useCookiesFile: video.Channel?.UseCookiesFile == true,
                        cancellation: stoppingToken);
                    break;
                case "Twitcasting":
                    await _ytdlpService.InitJobAsync(
                        url: $"https://twitcasting.tv/{video.ChannelId}/movie/{video.id}",
                        channelId: video.ChannelId,
                        useCookiesFile: false,
                        cancellation: stoppingToken);
                    break;
                case "Twitch":
                    var id = video.id.TrimStart('v');
                    await _ytdlpService.InitJobAsync(
                        url: $"https://www.twitch.tv/videos/{id}",
                        channelId: video.ChannelId,
                        useCookiesFile: false,
                        cancellation: stoppingToken);
                    break;
                case "FC2":
                    await _ytdlpService.InitJobAsync(
                        url: $"https://video.fc2.com/content/{video.id}",
                        channelId: video.ChannelId,
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
    }

    /// <summary>
    /// Check recordings status and return finished videos
    /// </summary>
    /// <param name="videoService"></param>
    /// <returns>Videos that finish recording.</returns>
    private async Task<Dictionary<Video, FileInfo>> MonitorRecordingVideosAsync(VideoService videoService, CancellationToken cancellation = default)
    {
        var finishedRecordingVideos = new Dictionary<Video, FileInfo>();
        var videos = videoService.GetVideosByStatus(VideoStatus.Recording)
             .Concat(videoService.GetVideosByStatus(VideoStatus.Downloading));
        foreach (var video in videos)
        {
            using var _ = LogContext.PushProperty("videoId", video.id);

            string prefix = video.Source switch
            {
                // It is possible for Youtube to use "-" at the beginning of an id, which can cause errors when using the id as a file name.
                // Therefore, we add "_" before the file name to avoid such issues.
                "Youtube" => "_" + video.id,
                "FC2" => video.ChannelId + (video.Timestamps.ActualStartTime ?? DateTime.Today).ToString("yyyy-MM-dd"),
                _ => video.id,
            };
            var file = await _sharedVolumeService.GetVideoFileInfoByPrefixAsync(prefix: prefix,
                                                                        delay: TimeSpan.FromMinutes(5),
                                                                        cancellation: cancellation);
            if (null != file)
            {
                _logger.LogInformation("Video recording finish {videoId}", video.id);
                finishedRecordingVideos.Add(video, file.Value);
            }
        }
        return finishedRecordingVideos;
    }
}