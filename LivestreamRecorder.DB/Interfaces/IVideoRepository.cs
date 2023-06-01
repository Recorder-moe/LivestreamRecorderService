using LivestreamRecorder.DB.Models;

namespace LivestreamRecorder.DB.Interfaces;

public interface IVideoRepository : ICosmosDbRepository<Video>
{
    IQueryable<Video> GetVideosByChannel(string channelId);
}
