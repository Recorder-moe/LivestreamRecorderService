using CodeHollow.FeedReader;
using LivestreamRecorderService.DB.Models;
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
            _logger.LogTrace("{Worker} starts...",nameof(MonitorWorker));

            #region DI
            using var scope = _serviceProvider.CreateScope();
            var videoService = scope.ServiceProvider.GetRequiredService<VideoService>();
            var rssService = scope.ServiceProvider.GetRequiredService<RSSService>();
            #endregion

            Dictionary<Channel, Feed> feeds = await rssService.ReadRSS();

            foreach (var kvp in feeds)
            {
                var (channel, feed) = (kvp.Key, kvp.Value);
                using var _ = LogContext.PushProperty("channelId", channel.id);

                if (channel.Source == "Youtube")
                {
                    foreach (var item in feed.Items)
                    {
                        await videoService.UpdateVideosDataAsync(channel, item);
                    }
                }
            }

            await videoService.SaveIfVideoContextHasChangesAsync();

            _logger.LogTrace("{Worker} ends. Sleep 5 minutes.", nameof(MonitorWorker));
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
