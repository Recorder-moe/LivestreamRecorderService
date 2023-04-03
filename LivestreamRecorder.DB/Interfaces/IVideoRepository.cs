using LivestreamRecorder.DB.Models;

namespace LivestreamRecorder.DB.Interfaces;

public interface IVideoRepository : ICosmosDbRepository<Video> { }
