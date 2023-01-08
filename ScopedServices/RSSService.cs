using CodeHollow.FeedReader;
using LivestreamRecorderService.DB.Interfaces;
using LivestreamRecorderService.DB.Models;

namespace LivestreamRecorderService.ScopedServices
{
    public class RSSService
    {
        private readonly ILogger<RSSService> _logger;
        private readonly IChannelRepository _channelRepository;

        public RSSService(
            ILogger<RSSService> logger,
            IChannelRepository channelRepository)
        {
            _logger = logger;
            _channelRepository = channelRepository;
        }

        public async Task<Dictionary<Channel, Feed>> ReadRSS()
        {
            _logger.LogInformation("Start to get monitoring channels");
            var channels = _channelRepository.GetMonitoringChannels();
            _logger.LogInformation("Get {count} monitoring channels. {channels}", channels.Count, string.Join(',', channels));

            _logger.LogInformation("Start to get RSS feeds");
            Dictionary<Channel, Feed> feeds = new();
            foreach (var channel in channels)
            {
                var url = $"https://www.youtube.com/feeds/videos.xml?channel_id={channel.id}";
                try
                {
                    var feed = await FeedReader.ReadAsync(url);
                    if (feed.Title != channel.ChannelName)
                    {
                        _logger.LogInformation("Update channel name from {oldName} to {newName}", channel.ChannelName, feed.Title);
                        channel.ChannelName = feed.Title;
                    }
                    feeds.Add(channel, feed);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Failed to get feed: {feed}", url);
                }
            }

            if (_channelRepository.HasChanged)
                await _channelRepository.SaveChangesAsync();

            return feeds;
        }

    }
}
