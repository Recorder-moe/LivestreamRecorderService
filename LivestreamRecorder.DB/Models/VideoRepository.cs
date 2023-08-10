#if COSMOSDB
using LivestreamRecorder.DB.CosmosDB;
using Microsoft.EntityFrameworkCore;
#elif COUCHDB
using LivestreamRecorder.DB.CouchDB;
#endif
using LivestreamRecorder.DB.Interfaces;

namespace LivestreamRecorder.DB.Models;

public class VideoRepository :
# if COSMOSDB
    CosmosDbRepository<Video>,
#elif COUCHDB
    CouchDbRepository<Video>,
#endif
    IVideoRepository
{
    public VideoRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }

    public Task<Video?> GetByVideoIdAndChannelId(string videoId, string channelId)
#if COUCHDB
        => base.GetById($"{channelId}:{videoId}");
#elif COSMOSDB
        => base.GetByPartitionKey(channelId)
               .Where(p => p.id == videoId)
               .SingleOrDefaultAsync();
#endif

    public IQueryable<Video> GetVideosByChannel(string channelId) => base.GetByPartitionKey(channelId);

    public override string CollectionName { get; } = "Videos";
}
