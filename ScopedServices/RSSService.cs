using CodeHollow.FeedReader;
using LivestreamRecorderService.DB.Interfaces;
using LivestreamRecorderService.DB.Models;

namespace LivestreamRecorderService.ScopedServices;

public class RSSService
{
    private readonly ILogger<RSSService> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IChannelRepository _channelRepository;

    public RSSService(
        ILogger<RSSService> logger,
        IUnitOfWork unitOfWork,
        IChannelRepository channelRepository)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _channelRepository = channelRepository;
    }

    public async Task<Feed?> ReadRSS(string url, CancellationToken cancellation = default)
    {
        _logger.LogTrace("Start to get RSS feed: {url}", url);
        Feed? feed = null;
        try
        {
            feed = await FeedReader.ReadAsync(url, cancellation);
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to get feed: {feed}", url);
        }
        return feed;
    }

    public void UpdateChannelName(Channel channel, Feed feed)
    {
        if (feed.Title != channel.ChannelName)
        {
            _logger.LogInformation("Update channel name from {oldName} to {newName}", channel.ChannelName, feed.Title);
            channel.ChannelName = feed.Title;
            _channelRepository.Update(channel);
            _unitOfWork.Commit();
        }
    }
}
