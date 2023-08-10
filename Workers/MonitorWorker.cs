using LivestreamRecorder.DB.Enums;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Interfaces.Job;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.ScopedServices;
using LivestreamRecorderService.ScopedServices.PlatformService;
using Microsoft.Extensions.Options;
using Serilog.Context;

namespace LivestreamRecorderService.Workers;

public class MonitorWorker : BackgroundService
{
    private readonly ILogger<MonitorWorker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TwitchOption _twitchOption;
    private const int _interval = 10;   // in seconds

    public MonitorWorker(
        ILogger<MonitorWorker> logger,
        IOptions<TwitchOption> twitchOption,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _twitchOption = twitchOption.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var _ = LogContext.PushProperty("Worker", nameof(MonitorWorker));
        _logger.LogTrace("{Worker} starts...", nameof(MonitorWorker));

        while (!stoppingToken.IsCancellationRequested)
        {
            using var __ = LogContext.PushProperty("WorkerRunId", $"{nameof(MonitorWorker)}_{DateTime.Now:yyyyMMddHHmmssfff}");

            #region DI
            using (var scope = _serviceProvider.CreateScope())
            {
                // KubernetesService needed to be initialized first
                var ___ = scope.ServiceProvider.GetRequiredService<IJobService>();
                var youtubeSerivce = scope.ServiceProvider.GetRequiredService<YoutubeService>();
                var twitcastingService = scope.ServiceProvider.GetRequiredService<TwitcastingService>();
                var fc2Service = scope.ServiceProvider.GetRequiredService<FC2Service>();
                var videoService = scope.ServiceProvider.GetRequiredService<VideoService>();
                #endregion

                await MonitorPlatform(youtubeSerivce, videoService, stoppingToken);
                await MonitorPlatform(twitcastingService, videoService, stoppingToken);
                await MonitorPlatform(fc2Service, videoService, stoppingToken);

                if (_twitchOption.Enabled)
                {
                    var twitchService = scope.ServiceProvider.GetRequiredService<TwitchService>();
                    await MonitorPlatform(twitchService, videoService, stoppingToken);
                }
            }

            _logger.LogTrace("{Worker} ends. Sleep {interval} seconds.", nameof(MonitorWorker), _interval);
            await Task.Delay(TimeSpan.FromSeconds(_interval), stoppingToken);
        }
    }

    private async Task MonitorPlatform(IPlatformService PlatformService, VideoService videoService, CancellationToken cancellation = default)
    {
        if (!PlatformService.StepInterval(_interval)) return;

        var channels = PlatformService.GetMonitoringChannels();
        _logger.LogTrace("Get {channelCount} channels for {platform}", channels.Count, PlatformService.PlatformName);
        foreach (var channel in channels)
        {
            await PlatformService.UpdateVideosDataAsync(channel, cancellation);
        }

        var videos = videoService.GetVideosBySource(PlatformService.PlatformName)
                                 .Where(p => p.Status == VideoStatus.Scheduled
                                             || p.Status == VideoStatus.Pending)
                                 .ToList();

        if (videos.Count == 0)
        {
            _logger.LogTrace("No Scheduled videos for {platform}", PlatformService.PlatformName);
            return;
        }
        else
        {
            _logger.LogDebug("Get {videoCount} Scheduled/Pending videos for {platform}", videos.Count, PlatformService.PlatformName);
        }

        foreach (var video in videos)
        {
            var channel = channels.SingleOrDefault(p => p.id == video.ChannelId);
            // Channel exists and is not monitoring
            if (null != channel
                && video.Status == VideoStatus.Scheduled
                && !channel.Monitoring)
            {
                await videoService.DeleteVideoAsync(video);
                _logger.LogInformation("Remove scheduled video {videoId} because channel {channelId} is not monitoring.", video.id, channel.id);
            }
            else
            {
                await PlatformService.UpdateVideoDataAsync(video, cancellation);
            }
        }
    }
}
