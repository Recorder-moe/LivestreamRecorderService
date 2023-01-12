using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.ScopedServices;
using Microsoft.Extensions.Options;
using Serilog.Context;

namespace LivestreamRecorderService;

public class MonitorWorker : BackgroundService
{
    private readonly ILogger<MonitorWorker> _logger;
    private readonly IServiceProvider _serviceProvider;

    private const int _interval = 10;   // in seconds

    public MonitorWorker(
        ILogger<MonitorWorker> logger,
        IOptions<AzureOption> options,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var _ = LogContext.PushProperty("Worker", nameof(MonitorWorker));
        _logger.LogTrace("{Worker} starts...", nameof(MonitorWorker));

        while (!stoppingToken.IsCancellationRequested)
        {
            using var __ = LogContext.PushProperty("WorkerRunId", $"{nameof(MonitorWorker)}_{DateTime.Now:yyyyMMddHHmmssfff}");

            #region DI
            using var scope = _serviceProvider.CreateScope();
            var youtubeSerivce = scope.ServiceProvider.GetRequiredService<YoutubeSerivce>();
            var twitcastingService = scope.ServiceProvider.GetRequiredService<TwitcastingService>();
            var twitchService = scope.ServiceProvider.GetRequiredService<TwitchSerivce>();
            #endregion

            await MonitorPlatform(youtubeSerivce, stoppingToken);
            await MonitorPlatform(twitcastingService, stoppingToken);
            await MonitorPlatform(twitchService, stoppingToken);

            _logger.LogTrace("{Worker} ends. Sleep {interval} seconds.", nameof(MonitorWorker), _interval);
            await Task.Delay(TimeSpan.FromSeconds(_interval), stoppingToken);
        }
    }

    private async Task MonitorPlatform(IPlatformSerivce PlatformService, CancellationToken cancellation = default)
    {
        if (!PlatformService.StepInterval(_interval)) return;

        var channels = PlatformService.GetMonitoringChannels();
        _logger.LogDebug("Get {channelCount} channels for {platform}", channels.Count, PlatformService.PlatformName);
        foreach (var channel in channels)
        {
            using var _ = LogContext.PushProperty("channelId", channel.id);

            await PlatformService.UpdateVideosDataAsync(channel, cancellation);
        }
    }
}
