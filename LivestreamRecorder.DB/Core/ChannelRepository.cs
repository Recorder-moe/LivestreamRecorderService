using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;

namespace LivestreamRecorder.DB.Core;

public class ChannelRepository : CosmosDbRepository<Channel>, IChannelRepository
{
    public ChannelRepository(UnitOfWork_Public unitOfWork) : base(unitOfWork)
    {
    }

    public IQueryable<Channel> GetChannelsBySource(string source) => base.GetByPartitionKey(source);

    public override string CollectionName { get; } = "Channels";
}
