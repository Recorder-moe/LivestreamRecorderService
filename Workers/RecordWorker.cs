using LivestreamRecorder.DB.Models;
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
        using IDisposable __ = LogContext.PushProperty("Worker", nameof(RecordWorker));

#if RELEASE
        logger.LogInformation("{Worker} will sleep 30 seconds to wait for {WorkerToWait} to start.", nameof(RecordWorker), nameof(MonitorWorker));
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
#endif

        logger.LogTrace("{Worker} starts...", nameof(RecordWorker));
        while (!stoppingToken.IsCancellationRequested)
        {
            using IDisposable ____ = LogContext.PushProperty("WorkerRunId", $"{nameof(RecordWorker)}_{DateTime.UtcNow:yyyyMMddHHmmssfff}");

            #region DI

            using (IServiceScope scope = serviceProvider.CreateScope())
            {
                VideoService videoService = scope.ServiceProvider.GetRequiredService<VideoService>();
                ChannelService channelService = scope.ServiceProvider.GetRequiredService<ChannelService>();

                #endregion

                await recordService.HandledFailedJobsAsync(videoService, stoppingToken);

                await recordService.CreateStartRecordJobAsync(videoService, channelService, stoppingToken);
                await recordService.CreateStartDownloadJobAsync(videoService, channelService, stoppingToken);

                List<Video> uploaded = await recordService.MonitorUploadedVideosAsync(videoService, stoppingToken);
                foreach (Video? video in uploaded)
                {
                    await recordService.ProcessUploadedVideoAsync(videoService, channelService, video, stoppingToken);

                    // Avoid concurrency requests
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }

                List<Video> finished = await recordService.MonitorRecordingDownloadingVideosAsync(videoService, stoppingToken);
                foreach (Video? video in finished)
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
