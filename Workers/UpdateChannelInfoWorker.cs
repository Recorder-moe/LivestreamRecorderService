using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.ScopedServices.PlatformService;
using Microsoft.Extensions.Options;
using Serilog.Context;

namespace LivestreamRecorderService.Workers;

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

#if RELEASE
        _logger.LogInformation("{Worker} will sleep 60 seconds avoid being overloaded with {WorkerToWait}.", nameof(RecordWorker), nameof(MonitorWorker));
        await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
#endif

        _logger.LogTrace("{Worker} starts...", nameof(UpdateChannelInfoWorker));

        while (!stoppingToken.IsCancellationRequested)
        {
            using var __ = LogContext.PushProperty("WorkerRunId", $"{nameof(UpdateChannelInfoWorker)}_{DateTime.Now:yyyyMMddHHmmssfff}");

            #region DI
            using var scope = _serviceProvider.CreateScope();
            YoutubeService youtubeSerivce = scope.ServiceProvider.GetRequiredService<YoutubeService>();
            FC2Service fC2Service = scope.ServiceProvider.GetRequiredService<FC2Service>();
            #endregion

            await UpdatePlatformAsync(youtubeSerivce, stoppingToken);
            await UpdatePlatformAsync(fC2Service, stoppingToken);

            _logger.LogTrace("{Worker} ends. Sleep 1 day.", nameof(UpdateChannelInfoWorker));
            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }

    private async Task UpdatePlatformAsync(IPlatformService platformService, CancellationToken stoppingToken = default)
    {
        var channels = platformService.GetMonitoringChannels();
        _logger.LogDebug("Get {channelCount} channels for {platform}", channels.Count, platformService.PlatformName);
        foreach (var channel in channels)
        {
            if (channel.AutoUpdateInfo != true) continue;

            await platformService.UpdateChannelDataAsync(channel, stoppingToken);
        }
    }
}
