#if COSMOSDB
using LivestreamRecorder.DB.CosmosDB;
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

    public IQueryable<Video> GetVideosByChannel(string channelId) => base.GetByPartitionKey(channelId);

    public override string CollectionName { get; } = "Videos";
}
