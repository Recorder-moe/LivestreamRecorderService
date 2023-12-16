using CodeHollow.FeedReader;
#if COSMOSDB
using LivestreamRecorder.DB.CosmosDB;
#elif COUCHDB
using LivestreamRecorder.DB.CouchDB;
#endif
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;

namespace LivestreamRecorderService.ScopedServices;

public class RSSService(
    ILogger<RSSService> logger,
    UnitOfWork_Public unitOfWork_Public,
    IChannelRepository channelRepository)
{
#pragma warning disable CA1859 // 盡可能使用具象類型以提高效能
    private readonly IUnitOfWork _unitOfWork_Public = unitOfWork_Public;
#pragma warning restore CA1859 // 盡可能使用具象類型以提高效能

    public async Task<Feed?> ReadRSSAsync(string url, CancellationToken cancellation = default)
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
            _unitOfWork_Public.Commit();
        }
    }
}
