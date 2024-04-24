using CodeHollow.FeedReader;
#if COSMOSDB
using LivestreamRecorder.DB.CosmosDB;
#elif COUCHDB
using LivestreamRecorder.DB.CouchDB;
#endif
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;

namespace LivestreamRecorderService.ScopedServices;

public class RssService(
    ILogger<RssService> logger,
    // ReSharper disable once SuggestBaseTypeForParameterInConstructor
    UnitOfWork_Public unitOfWorkPublic,
    IChannelRepository channelRepository)
{
#pragma warning disable CA1859 // 盡可能使用具象類型以提高效能
    private readonly IUnitOfWork _unitOfWorkPublic = unitOfWorkPublic;
#pragma warning restore CA1859 // 盡可能使用具象類型以提高效能

    public async Task<Feed?> ReadRssAsync(string url, CancellationToken cancellation = default)
    {
        logger.LogTrace("Start to get RSS feed: {url}", url);
        Feed? feed = null;
        try
        {
            feed = await FeedReader.ReadAsync(url, cancellation);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to get feed: {feed}", url);
        }

        return feed;
    }

    public void UpdateChannelName(Channel channel, Feed feed)
    {
        if (feed.Title != channel.ChannelName)
        {
            logger.LogInformation("Update channel name from {oldName} to {newName}", channel.ChannelName, feed.Title);
            channel.ChannelName = feed.Title;
            channelRepository.AddOrUpdateAsync(channel);
            _unitOfWorkPublic.Commit();
        }
    }
}
