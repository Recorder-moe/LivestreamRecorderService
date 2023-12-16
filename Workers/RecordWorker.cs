using LivestreamRecorderService.ScopedServices;
using LivestreamRecorderService.SingletonServices;
using Serilog.Context;

namespace LivestreamRecorderService.Workers;

public class RecordWorker(
    ILogger<RecordWorker> logger,
    RecordService recordService,
    IServiceProvider serviceProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var __ = LogContext.PushProperty("Worker", nameof(RecordWorker));

#if RELEASE
        logger.LogInformation("{Worker} will sleep 30 seconds to wait for {WorkerToWait} to start.", nameof(RecordWorker), nameof(MonitorWorker));
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
#endif

        logger.LogTrace("{Worker} starts...", nameof(RecordWorker));
        while (!stoppingToken.IsCancellationRequested)
        {
            using var ____ = LogContext.PushProperty("WorkerRunId", $"{nameof(RecordWorker)}_{DateTime.Now:yyyyMMddHHmmssfff}");
            #region DI
            using (var scope = serviceProvider.CreateScope())
            {
                var videoService = scope.ServiceProvider.GetRequiredService<VideoService>();
                var channelService = scope.ServiceProvider.GetRequiredService<ChannelService>();
                #endregion

                await recordService.HandledFailedJobsAsync(videoService, stoppingToken);

                await recordService.CreateStartRecordJobAsync(videoService, channelService, stoppingToken);
                await recordService.CreateStartDownloadJobAsync(videoService, channelService, stoppingToken);

                var uploaded = await recordService.MonitorUploadedVideosAsync(videoService, stoppingToken);
                foreach (var video in uploaded)
                {
                    await recordService.ProcessUploadedVideoAsync(videoService, channelService, video, stoppingToken);

                    // Avoid concurrency requests
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }

                var finished = await recordService.MonitorRecordingDownloadingVideosAsync(videoService, stoppingToken);
                foreach (var video in finished)
                {
                    await recordService.PcocessFinishedVideoAsync(videoService, channelService, video, stoppingToken);

                    // Avoid concurrency requests
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
            }

            logger.LogTrace("{Worker} ends. Sleep 5 minutes.", nameof(RecordWorker));
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}