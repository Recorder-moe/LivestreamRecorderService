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
        _context.Entry(entity)
                .Collection(channel => channel.Videos)
                .Load();
        return entity;
    }

    public override string CollectionName { get; } = "Channels";
}
