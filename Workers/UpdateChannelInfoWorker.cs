#if COSMOSDB
using LivestreamRecorder.DB.CosmosDB;
#elif COUCHDB
using LivestreamRecorder.DB.CouchDB;
#endif
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.ScopedServices.PlatformService;
using Omu.ValueInjecter;
using Serilog.Context;

namespace LivestreamRecorderService.Workers;

public class UpdateChannelInfoWorker(
    ILogger<UpdateChannelInfoWorker> logger,
    IServiceProvider serviceProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var _ = LogContext.PushProperty("Worker", nameof(UpdateChannelInfoWorker));

        await HandleOldChannelIdAsync();

#if RELEASE
        logger.LogInformation("{Worker} will sleep 60 seconds avoid being overloaded with {WorkerToWait}.", nameof(RecordWorker), nameof(MonitorWorker));
        await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
#endif

        logger.LogTrace("{Worker} starts...", nameof(UpdateChannelInfoWorker));

        while (!stoppingToken.IsCancellationRequested)
        {
            using var __ = LogContext.PushProperty("WorkerRunId", $"{nameof(UpdateChannelInfoWorker)}_{DateTime.Now:yyyyMMddHHmmssfff}");

            #region DI
            using var scope = serviceProvider.CreateScope();
            YoutubeService youtubeSerivce = scope.ServiceProvider.GetRequiredService<YoutubeService>();
            FC2Service fC2Service = scope.ServiceProvider.GetRequiredService<FC2Service>();
            #endregion

            await UpdatePlatformAsync(youtubeSerivce, stoppingToken);
            await UpdatePlatformAsync(fC2Service, stoppingToken);

            logger.LogTrace("{Worker} ends. Sleep 1 day.", nameof(UpdateChannelInfoWorker));
            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }

    /// <summary>
    /// Handle old channel id. This is a temporary workaround for the channel id changes.
    /// </summary>
    /// <returns></returns>
    private async Task HandleOldChannelIdAsync()
    {
        logger.LogInformation("Start to handle old channel id.");
        using var scope = serviceProvider.CreateScope();
        var channelRepository = scope.ServiceProvider.GetRequiredService<IChannelRepository>();
#pragma warning disable CA1859 // 盡可能使用具象類型以提高效能
        IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<UnitOfWork_Public>();
#pragma warning restore CA1859 // 盡可能使用具象類型以提高效能

        var channels = await channelRepository.GetChannelsBySourceAsync("Twitcasting");
        if (channels.Count == 0)
        {
            logger.LogInformation("No old channel id needs to update.");
            return;
        }

        int count = 0;
        foreach (var channel in channels)
        {
            if (!channel.id.StartsWith('T'))
            {
                var newChannel = Mapper.Map<Channel>(channel);
                newChannel.id = NameHelper.ChangeId.ChannelId.DatabaseType(channel.id, "Twitcasting");
                await channelRepository.DeleteAsync(channel);
                await channelRepository.AddOrUpdateAsync(newChannel);
                count++;
            }
        }
        unitOfWork.Commit();

        logger.LogInformation("Handled {count} old channel id.", count);
    }

    private async Task UpdatePlatformAsync(IPlatformService platformService, CancellationToken stoppingToken = default)
    {
        var channels = await platformService.GetMonitoringChannels();
        logger.LogDebug("Get {channelCount} channels for {platform}", channels.Count, platformService.PlatformName);
        foreach (var channel in channels)
        {
            if (channel.AutoUpdateInfo != true) continue;

            await platformService.UpdateChannelDataAsync(channel, stoppingToken);
        }
    }
}
