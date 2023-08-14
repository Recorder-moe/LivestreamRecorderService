using LivestreamRecorder.DB.Models;

namespace LivestreamRecorder.DB.Interfaces;

public interface IVideoRepository : IRepository<Video>
{
    Task<Video?> GetVideoByIdAndChannelIdAsync(string videoId, string channelId);
    IQueryable<Video> GetVideosByChannel(string channelId);
}
