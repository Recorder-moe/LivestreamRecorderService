#if COSMOSDB
using LivestreamRecorder.DB.CosmosDB;
using Microsoft.EntityFrameworkCore;
#elif COUCHDB
using LivestreamRecorder.DB.CouchDB;
#endif
using LivestreamRecorder.DB.Interfaces;

namespace LivestreamRecorder.DB.Models;

public class ChannelRepository :
#if COSMOSDB
    CosmosDbRepository<Channel>,
#elif COUCHDB
    CouchDbRepository<Channel>,
#endif
    IChannelRepository
{
    public ChannelRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }

    public Task<Channel?> GetByChannelIdAndSource(string channelId, string source)
#if COUCHDB
        => base.GetById($"{source}:{channelId}");
#elif COSMOSDB
        => base.GetByPartitionKey(source)
               .Where(p => p.id == channelId)
               .SingleOrDefaultAsync();
#endif

    public IQueryable<Channel> GetChannelsBySource(string source) => base.GetByPartitionKey(source);

    public override string CollectionName { get; } = "Channels";
}
