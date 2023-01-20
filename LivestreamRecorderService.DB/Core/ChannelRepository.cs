using LivestreamRecorderService.DB.Interfaces;
using LivestreamRecorderService.DB.Models;

namespace LivestreamRecorderService.DB.Core;

public class ChannelRepository : CosmosDbRepository<Channel>, IChannelRepository
{
    public ChannelRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }

    public override Channel LoadRelatedData(Channel entity)
    {
        UnitOfWork.Context.Entry(entity)
                          .Collection(channel => channel.Videos)
                          .Load();
        UnitOfWork.Context.Entry(entity)
                          .Reference(channel => channel.LatestVideo)
                          .Load();
        return entity;
    }

    public IQueryable<Channel> GetMonitoringChannels() => Where(p => p.Monitoring);

    public override string CollectionName { get; } = "Channels";
}
