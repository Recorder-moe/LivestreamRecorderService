using LivestreamRecorder.DB.Models;

namespace LivestreamRecorder.DB.Interfaces;

public interface IChannelRepository : IRepository<Channel>
{
    Task<Channel?> GetChannelByIdAndSourceAsync(string channelId, string source);
    IQueryable<Channel> GetChannelsBySource(string source);
}
