using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.ScopedServices;
using Microsoft.Extensions.Options;
using Serilog.Context;

namespace LivestreamRecorderService;

public class ChannelInfoWorker : BackgroundService
{
    private readonly ILogger<ChannelInfoWorker> _logger;
    private readonly IServiceProvider _serviceProvider;

    public ChannelInfoWorker(
        ILogger<ChannelInfoWorker> logger,
        IOptions<AzureOption> options,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var _ = LogContext.PushProperty("Worker", nameof(ChannelInfoWorker));
        _logger.LogTrace("{Worker} starts...", nameof(ChannelInfoWorker));

        while (!stoppingToken.IsCancellationRequested)
        {
            using var __ = LogContext.PushProperty("WorkerRunId", $"{nameof(ChannelInfoWorker)}_{DateTime.Now:yyyyMMddHHmmssfff}");

            #region DI
            using var scope = _serviceProvider.CreateScope();
            YoutubeSerivce youtubeSerivce = scope.ServiceProvider.GetRequiredService<YoutubeSerivce>();
            #endregion

            var channels = youtubeSerivce.GetMonitoringChannels();
            _logger.LogDebug("Get {channelCount} channels for {platform}", channels.Count, youtubeSerivce.PlatformName);
            foreach (var channel in channels)
            {
                await youtubeSerivce.UpdateChannelData(channel, stoppingToken);
            }

            _logger.LogTrace("{Worker} ends. Sleep 1 day.", nameof(ChannelInfoWorker));
            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }
}
