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
        while (!stoppingToken.IsCancellationRequested)
        {
            using var __ = LogContext.PushProperty("WorkerRunId", $"{nameof(MonitorWorker)}_{DateTime.Now:yyyyMMddHHmmssfff}");
            using var _ = LogContext.PushProperty("Worker", nameof(MonitorWorker));
            _logger.LogTrace("{Worker} starts...", nameof(MonitorWorker));

            #region DI
            using var scope = _serviceProvider.CreateScope();
            var youtubeSerivce = scope.ServiceProvider.GetRequiredService<YoutubeSerivce>();
            var twitcastingService = scope.ServiceProvider.GetRequiredService<TwitcastingService>();
            #endregion

            await MonitorPlatform(youtubeSerivce);
            await MonitorPlatform(twitcastingService);

            _logger.LogTrace("{Worker} ends. Sleep 5 minutes.", nameof(MonitorWorker));
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private async Task MonitorPlatform(IPlatformSerivce PlatformService)
    {
        var channels = PlatformService.GetMonitoringChannels();
        _logger.LogDebug("Get {channelCount} channels for {platform}", channels.Count, PlatformService.PlatformName);
        foreach (var channel in channels)
        {
            using var _ = LogContext.PushProperty("channelId", channel.id);

            await PlatformService.UpdateVideosDataAsync(channel);
        }
    }
}
