using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.ScopedServices.PlatformService;
using Serilog.Context;

namespace LivestreamRecorderService.Workers;

public class UpdateChannelInfoWorker(
    ILogger<UpdateChannelInfoWorker> logger,
    IServiceProvider serviceProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using IDisposable _ = LogContext.PushProperty("Worker", nameof(UpdateChannelInfoWorker));

#if RELEASE
        logger.LogInformation("{Worker} will sleep 60 seconds avoid being overloaded with {WorkerToWait}.", nameof(RecordWorker), nameof(MonitorWorker));
        await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
#endif

        logger.LogTrace("{Worker} starts...", nameof(UpdateChannelInfoWorker));

        while (!stoppingToken.IsCancellationRequested)
        {
            using IDisposable __ = LogContext.PushProperty("WorkerRunId", $"{nameof(UpdateChannelInfoWorker)}_{DateTime.UtcNow:yyyyMMddHHmmssfff}");

            #region DI

            using IServiceScope scope = serviceProvider.CreateScope();
            YoutubeService youtubeSerivce = scope.ServiceProvider.GetRequiredService<YoutubeService>();
            Fc2Service fC2Service = scope.ServiceProvider.GetRequiredService<Fc2Service>();

            #endregion

            await UpdatePlatformAsync(youtubeSerivce, stoppingToken);
            await UpdatePlatformAsync(fC2Service, stoppingToken);

            logger.LogTrace("{Worker} ends. Sleep 1 day.", nameof(UpdateChannelInfoWorker));
            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }

    private async Task UpdatePlatformAsync(IPlatformService platformService, CancellationToken stoppingToken = default)
    {
        List<Channel> channels = await platformService.GetMonitoringChannels();
        logger.LogDebug("Get {channelCount} channels for {platform}", channels.Count, platformService.PlatformName);
        foreach (Channel? channel in channels)
        {
            if (channel.AutoUpdateInfo != true) continue;

            await platformService.UpdateChannelDataAsync(channel, stoppingToken);
        }
    }
}
