using LivestreamRecorder.DB.Models;

namespace LivestreamRecorder.DB.Interfaces;

public interface IChannelRepository : ICosmosDbRepository<Channel>
{
    IQueryable<Channel> GetMonitoringChannels();
}
