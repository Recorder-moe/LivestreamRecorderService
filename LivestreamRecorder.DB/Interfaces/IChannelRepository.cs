using LivestreamRecorder.DB.Models;

namespace LivestreamRecorder.DB.Interfaces;

public interface IChannelRepository : IRepository<Channel>
{
    IQueryable<Channel> GetChannelsBySource(string source);
}
