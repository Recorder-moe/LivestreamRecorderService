using LivestreamRecorder.DB.Enums;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Interfaces.Job;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.ScopedServices;
using LivestreamRecorderService.ScopedServices.PlatformService;
using Microsoft.Extensions.Options;
using Serilog.Context;

namespace LivestreamRecorderService.Workers;

public class MonitorWorker(
    ILogger<MonitorWorker> logger,
    IOptions<TwitchOption> twitchOption,
    IServiceProvider serviceProvider) : BackgroundService
{
    private readonly TwitchOption _twitchOption = twitchOption.Value;
    private const int _interval = 10;   // in seconds

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var _ = LogContext.PushProperty("Worker", nameof(MonitorWorker));
        logger.LogTrace("{Worker} starts...", nameof(MonitorWorker));

        while (!stoppingToken.IsCancellationRequested)
        {
            using var __ = LogContext.PushProperty("WorkerRunId", $"{nameof(MonitorWorker)}_{DateTime.UtcNow:yyyyMMddHHmmssfff}");

            #region DI
            using (var scope = serviceProvider.CreateScope())
            {
                // KubernetesService needed to be initialized first
                var ___ = scope.ServiceProvider.GetRequiredService<IJobService>();
                var youtubeSerivce = scope.ServiceProvider.GetRequiredService<YoutubeService>();
                var twitcastingService = scope.ServiceProvider.GetRequiredService<TwitcastingService>();
                var fc2Service = scope.ServiceProvider.GetRequiredService<FC2Service>();
                var videoService = scope.ServiceProvider.GetRequiredService<VideoService>();
                #endregion

                await MonitorPlatformAsync(youtubeSerivce, videoService, stoppingToken);
                await MonitorPlatformAsync(twitcastingService, videoService, stoppingToken);
                await MonitorPlatformAsync(fc2Service, videoService, stoppingToken);

                if (_twitchOption.Enabled)
                {
                    var twitchService = scope.ServiceProvider.GetRequiredService<TwitchService>();
                    await MonitorPlatformAsync(twitchService, videoService, stoppingToken);
                }
            }

            logger.LogTrace("{Worker} ends. Sleep {interval} seconds.", nameof(MonitorWorker), _interval);
            await Task.Delay(TimeSpan.FromSeconds(_interval), stoppingToken);
        }
    }

    private async Task MonitorPlatformAsync(IPlatformService platformService, VideoService videoService, CancellationToken cancellation = default)
    {
        if (!platformService.StepInterval(_interval)) return;

        var channels = await platformService.GetMonitoringChannels();
        logger.LogTrace("Get {channelCount} channels for {platform}", channels.Count, platformService.PlatformName);
        await UpdateVideosDataFromSource();
        await UpdateScheduledVideosStatus();

        async Task UpdateVideosDataFromSource()
        {
            foreach (var channel in channels)
            {
                await platformService.UpdateVideosDataAsync(channel, cancellation);
            }
        }

        async Task UpdateScheduledVideosStatus()
        {
            var videos = videoService.GetVideosBySource(platformService.PlatformName)
                                     .Where(p => p.Status == VideoStatus.Scheduled
                                                 || p.Status == VideoStatus.Pending)
                                     .ToList();

            if (videos.Count == 0)
            {
                logger.LogTrace("No Scheduled videos for {platform}", platformService.PlatformName);
                return;
            }

            logger.LogDebug("Get {videoCount} Scheduled/Pending videos for {platform}", videos.Count, platformService.PlatformName);

            foreach (var video in videos)
            {
                var channel = channels.SingleOrDefault(p => p.id == video.ChannelId);
                // Channel exists and is not monitoring
                if (null != channel
                    && video.Status == VideoStatus.Scheduled
                    && !channel.Monitoring)
                {
                    await videoService.DeleteVideoAsync(video);
                    logger.LogInformation("Remove scheduled video {videoId} because channel {channelId} is not monitoring.", video.id, channel.id);
                }
                else
                {
                    await platformService.UpdateVideoDataAsync(video, cancellation);
                }
            }
        }
    }
}
