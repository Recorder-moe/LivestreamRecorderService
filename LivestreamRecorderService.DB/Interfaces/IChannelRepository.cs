using LivestreamRecorderService.DB.Models;

namespace LivestreamRecorderService.DB.Interfaces;

public interface IChannelRepository : ICosmosDbRepository<Channel>
{
    IQueryable<Channel> GetMonitoringChannels();
}
