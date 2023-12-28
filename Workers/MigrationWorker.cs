#if COSMOSDB
using LivestreamRecorder.DB.CosmosDB;
#elif COUCHDB
using LivestreamRecorder.DB.CouchDB;
#endif
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using Omu.ValueInjecter;

namespace LivestreamRecorderService.Workers;

public class MigrationWorker(
    ILogger<MigrationWorker> logger,
    IServiceProvider serviceProvider) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await HandleOldChannelIdAsync();
        await HandleOldVideoIdAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Handle old channel id. This is a temporary workaround for the channel id changes.
    /// </summary>
    /// <returns></returns>
    private async Task HandleOldChannelIdAsync()
    {
        logger.LogInformation("Start to handle old channel id.");
        using var scope = serviceProvider.CreateScope();
        var channelRepository = scope.ServiceProvider.GetRequiredService<IChannelRepository>();
        var videoRepository = scope.ServiceProvider.GetRequiredService<IVideoRepository>();
#pragma warning disable CA1859 // 盡可能使用具象類型以提高效能
        IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<UnitOfWork_Public>();
#pragma warning restore CA1859 // 盡可能使用具象類型以提高效能

        await handle("Twitch", "TW");
        await handle("Twitcasting", "TC");
        await handle("FC2", "FC");

        async Task handle(string platformName, string prefix)
        {
            var channels = (await channelRepository.GetChannelsBySourceAsync(platformName))
                                                   .Where(p => !p.id.StartsWith(prefix))
                                                   .ToList();
            if (channels.Count == 0)
            {
                logger.LogInformation("No old {platform} channels needs to update.", platformName);
                return;
            }

            foreach (var channel in channels)
            {
                var newChannel = Mapper.Map<Channel>(channel);
#if COUCHDB
                newChannel.id = NameHelper.ChangeId.ChannelId.DatabaseType(channel.id, platformName);
                newChannel.Rev = null;
#else
                newChannel.id = Guid.NewGuid().ToString().Replace("-", "");
#endif

#if COUCHDB
                if (!channelRepository.Exists(newChannel.Id))
#endif
                {
                    await channelRepository.AddOrUpdateAsync(newChannel);
                }

                var videos = await videoRepository.GetVideosByChannelAsync(channel.id);
                foreach (var video in videos)
                {
                    var newVideo = Mapper.Map<Video>(video);
                    newVideo.ChannelId = newChannel.id;

#if COUCHDB
                    // Video in CouchDB uses ChannelId as partition key so it is a new entity.
                    // We need to clear the Rev to avoid conflict.
                    newVideo.Rev = null;
#else
                    newVideo.id = Guid.NewGuid().ToString().Replace("-", "");
#endif

#if COUCHDB
                    if (!videoRepository.Exists(newVideo.Id))
#endif
                    {
                        await videoRepository.AddOrUpdateAsync(newVideo);
                    }

                    await videoRepository.DeleteAsync(video);
                }
                newChannel.LatestVideoId = null != newChannel.LatestVideoId
                    ? NameHelper.ChangeId.VideoId.DatabaseType(newChannel.LatestVideoId, platformName)
                    : null;
                logger.LogInformation("Update {videoCount} videos channel id from {oldChannelId} to {newChannelId}.", videos.Count, channel.id, newChannel.id);
                await channelRepository.DeleteAsync(channel);
            }
            unitOfWork.Commit();
        }
    }

    /// <summary>
    /// Handle old channel id. This is a temporary workaround for the channel id changes.
    /// </summary>
    /// <returns></returns>
    private async Task HandleOldVideoIdAsync()
    {
        logger.LogInformation("Start to handle old video id.");
        using var scope = serviceProvider.CreateScope();
        var channelRepository = scope.ServiceProvider.GetRequiredService<IChannelRepository>();
        var videoRepository = scope.ServiceProvider.GetRequiredService<IVideoRepository>();
#pragma warning disable CA1859 // 盡可能使用具象類型以提高效能
        IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<UnitOfWork_Public>();
#pragma warning restore CA1859 // 盡可能使用具象類型以提高效能

        await handle("Twitch", "TW");
        await handle("Twitcasting", "TC");
        await handle("FC2", "FC");

        async Task handle(string platformName, string prefix)
        {
            var channels = await channelRepository.GetChannelsBySourceAsync(platformName);
            var videos = new List<Video>();
            foreach (var channel in channels)
            {
                videos = [
                    .. videos,
                    .. (await videoRepository.GetVideosByChannelAsync(channel.id))
                                             .Where(p => !p.id.StartsWith(prefix))
                ];
            }

            if (videos.Count == 0)
            {
                logger.LogInformation("No old {platform} videos needs to update.", platformName);
                return;
            }

            foreach (var video in videos)
            {
                var newVideo = Mapper.Map<Video>(video);
#if COUCHDB
                newVideo.id = NameHelper.ChangeId.VideoId.DatabaseType(video.id, platformName);
                newVideo.Rev = null;
#else
                newVideo.id = Guid.NewGuid().ToString().Replace("-", "");
#endif

#if COUCHDB
                if (!videoRepository.Exists(newVideo.Id))
#endif
                {
                    await videoRepository.AddOrUpdateAsync(newVideo);
                }

                await videoRepository.DeleteAsync(video);
            }
            unitOfWork.Commit();
            logger.LogInformation("Update {videoCount} videos id for {platform}.", videos.Count, platformName);
        }
    }
}
