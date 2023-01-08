using LivestreamRecorderService.DB.Interfaces;
using LivestreamRecorderService.DB.Models;

namespace LivestreamRecorderService.DB.Core;

public class ChannelRepository : CosmosDbRepository<Channel>, IChannelRepository
{
    public ChannelRepository(PublicContext context) : base(context)
    {
    }

    public override Channel LoadRelatedData(Channel entity)
    {
        context.Entry(entity)
                .Collection(channel => channel.Videos)
                .Load();
        return entity;
    }

    public List<Channel> GetMonitoringChannels() 
        => Where(p => p.Monitoring).ToList();

    public override string CollectionName { get; } = "Channels";
}
