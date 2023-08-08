using LivestreamRecorder.DB.Models;

namespace LivestreamRecorder.DB.Interfaces;

public interface IVideoRepository : IRepository<Video>
{
    IQueryable<Video> GetVideosByChannel(string channelId);
}
