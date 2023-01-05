using LivestreamRecorderService.DB.Models;

namespace LivestreamRecorderService.DB.Interfaces
{
    public interface IVideoRepository : ICosmosDbRepository<Video> { }
}
