using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.ScopedServices;
using Microsoft.Extensions.Options;
using Serilog.Context;

namespace LivestreamRecorderService;

public class UpdateChannelInfoWorker : BackgroundService
{
    private readonly ILogger<UpdateChannelInfoWorker> _logger;
    private readonly IServiceProvider _serviceProvider;

    public UpdateChannelInfoWorker(
        ILogger<UpdateChannelInfoWorker> logger,
        IOptions<AzureOption> options,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var _ = LogContext.PushProperty("Worker", nameof(UpdateChannelInfoWorker));
        _logger.LogTrace("{Worker} starts...", nameof(UpdateChannelInfoWorker));

        while (!stoppingToken.IsCancellationRequested)
        {
            using var __ = LogContext.PushProperty("WorkerRunId", $"{nameof(UpdateChannelInfoWorker)}_{DateTime.Now:yyyyMMddHHmmssfff}");

            #region DI
            using var scope = _serviceProvider.CreateScope();
            YoutubeSerivce youtubeSerivce = scope.ServiceProvider.GetRequiredService<YoutubeSerivce>();
            #endregion

            #region Youtube
            var channels = youtubeSerivce.GetMonitoringChannels();
            _logger.LogDebug("Get {channelCount} channels for {platform}", channels.Count, youtubeSerivce.PlatformName);
            foreach (var channel in channels)
            {
                await youtubeSerivce.UpdateChannelData(channel, stoppingToken);
            }
            #endregion

            _logger.LogTrace("{Worker} ends. Sleep 1 day.", nameof(UpdateChannelInfoWorker));
            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }
}
