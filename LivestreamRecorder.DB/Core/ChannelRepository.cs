using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;

namespace LivestreamRecorder.DB.Core;

public class ChannelRepository : CosmosDbRepository<Channel>, IChannelRepository
{
    public ChannelRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }

    public IQueryable<Channel> GetMonitoringChannels() => Where(p => p.Monitoring);

    public override string CollectionName { get; } = "Channels";
}
