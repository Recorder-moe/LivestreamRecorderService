using LivestreamRecorderService.ScopedServices;
using LivestreamRecorderService.SingletonServices;
using Serilog.Context;

namespace LivestreamRecorderService.Workers;

public class RecordWorker : BackgroundService
{
    private readonly ILogger<RecordWorker> _logger;
    private readonly RecordService _recordService;
    private readonly IServiceProvider _serviceProvider;

    public RecordWorker(
        ILogger<RecordWorker> logger,
        RecordService recordService,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _recordService = recordService;
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

                await _recordService.HandledFailedJobsAsync(videoService, stoppingToken);

                await _recordService.CreateStartRecordJobAsync(videoService, stoppingToken);
                await _recordService.CreateStartDownloadJobAsync(videoService, stoppingToken);

                var uploaded = await _recordService.MonitorUploadedVideosAsync(videoService, stoppingToken);
                foreach (var video in uploaded)
                {
                    await _recordService.ProcessUploadedVideo(videoService, video, stoppingToken);

                    // Avoid concurrency requests
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }

                var finished = await _recordService.MonitorRecordingVideosAsync(videoService, stoppingToken);
                foreach (var video in finished)
                {
                    await _recordService.PcocessFinishedVideo(videoService, channelService, video, stoppingToken);

                    // Avoid concurrency requests
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
            }

            _logger.LogTrace("{Worker} ends. Sleep 5 minutes.", nameof(RecordWorker));
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}