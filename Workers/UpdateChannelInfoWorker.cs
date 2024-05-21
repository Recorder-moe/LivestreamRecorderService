using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.ScopedServices.PlatformService;
using Microsoft.Extensions.Options;
using Serilog.Context;

namespace LivestreamRecorderService.Workers;

public class UpdateChannelInfoWorker(
    ILogger<UpdateChannelInfoWorker> logger,
    IServiceProvider serviceProvider,
    IOptions<TwitchOption> twitchOptions) : BackgroundService
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
            YoutubeService youtubeService = scope.ServiceProvider.GetRequiredService<YoutubeService>();
            TwitcastingService twitcastingService = scope.ServiceProvider.GetRequiredService<TwitcastingService>();
            Fc2Service fC2Service = scope.ServiceProvider.GetRequiredService<Fc2Service>();

            #endregion

            await UpdatePlatformAsync(youtubeService, stoppingToken);
            await UpdatePlatformAsync(twitcastingService, stoppingToken);
            await UpdatePlatformAsync(fC2Service, stoppingToken);

            if (twitchOptions.Value.Enabled)
            {
                TwitchService twitchService = scope.ServiceProvider.GetRequiredService<TwitchService>();
                await UpdatePlatformAsync(twitchService, stoppingToken);
            }

            logger.LogTrace("{Worker} ends. Sleep 1 day.", nameof(UpdateChannelInfoWorker));
            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }

    private async Task UpdatePlatformAsync(IPlatformService platformService, CancellationToken stoppingToken = default)
    {
        var channels = (await platformService.GetAllChannels())
                       .Where(p => p.AutoUpdateInfo == true)
                       .ToList();

        logger.LogDebug("Get {channelCount} channels for {platform}", channels.Count, platformService.PlatformName);
        foreach (Channel channel in channels) await platformService.UpdateChannelDataAsync(channel, stoppingToken);
    }
}
