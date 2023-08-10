using LivestreamRecorder.DB.Models;

namespace LivestreamRecorder.DB.Interfaces;

public interface IChannelRepository : IRepository<Channel>
{
    Task<Channel?> GetByChannelIdAndSource(string channelId, string source);
    IQueryable<Channel> GetChannelsBySource(string source);
}
