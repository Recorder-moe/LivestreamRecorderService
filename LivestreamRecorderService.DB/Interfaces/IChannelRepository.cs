using LivestreamRecorderService.DB.Models;

namespace LivestreamRecorderService.DB.Interfaces
{
    public interface IChannelRepository : ICosmosDbRepository<Channel>
    {
        List<Channel> GetMonitoringChannels();
    }
}
