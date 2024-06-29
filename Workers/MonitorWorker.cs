using LivestreamRecorder.DB.Enums;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Interfaces;
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
    private const int Interval = 10; // in seconds
    private readonly TwitchOption _twitchOption = twitchOption.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using IDisposable _ = LogContext.PushProperty("Worker", nameof(MonitorWorker));
        logger.LogTrace("{Worker} starts...", nameof(MonitorWorker));

        while (!stoppingToken.IsCancellationRequested)
        {
            using IDisposable __ = LogContext.PushProperty("WorkerRunId", $"{nameof(MonitorWorker)}_{DateTime.UtcNow:yyyyMMddHHmmssfff}");

            #region DI

            using (IServiceScope scope = serviceProvider.CreateScope())
            {
                // KubernetesService needed to be initialized first
                IJobService ___ = scope.ServiceProvider.GetRequiredService<IJobService>();
                YoutubeService youtubeService = scope.ServiceProvider.GetRequiredService<YoutubeService>();
                TwitcastingService twitcastingService = scope.ServiceProvider.GetRequiredService<TwitcastingService>();
                Fc2Service fc2Service = scope.ServiceProvider.GetRequiredService<Fc2Service>();
                VideoService videoService = scope.ServiceProvider.GetRequiredService<VideoService>();

                #endregion

                await MonitorPlatformAsync(youtubeService, videoService, stoppingToken);
                await MonitorPlatformAsync(twitcastingService, videoService, stoppingToken);
                await MonitorPlatformAsync(fc2Service, videoService, stoppingToken);

                if (_twitchOption.Enabled)
                {
                    TwitchService twitchService = scope.ServiceProvider.GetRequiredService<TwitchService>();
                    await MonitorPlatformAsync(twitchService, videoService, stoppingToken);
                }
            }

            logger.LogTrace("{Worker} ends. Sleep {interval} seconds.", nameof(MonitorWorker), Interval);
            await Task.Delay(TimeSpan.FromSeconds(Interval), stoppingToken);
        }
    }

    private async Task MonitorPlatformAsync(IPlatformService platformService, VideoService videoService, CancellationToken cancellation = default)
    {
        if (!platformService.StepInterval(Interval)) return;

        List<Channel> channels = await platformService.GetMonitoringChannels();
        logger.LogTrace("Get {channelCount} channels for {platform}", channels.Count, platformService.PlatformName);
        await updateVideosDataFromSource();
        await updateScheduledVideosStatus();
        return;

        async Task updateVideosDataFromSource()
        {
            foreach (Channel? channel in channels) await platformService.UpdateVideosDataAsync(channel, cancellation);
        }

        async Task updateScheduledVideosStatus()
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

            foreach (Video? video in videos)
            {
                Channel? channel = channels.SingleOrDefault(p => p.id == video.ChannelId);
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
