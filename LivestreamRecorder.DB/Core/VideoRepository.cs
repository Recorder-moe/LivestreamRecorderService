using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;

namespace LivestreamRecorder.DB.Core;

public class VideoRepository : CosmosDbRepository<Video>, IVideoRepository
{
    public VideoRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }

    public override string CollectionName { get; } = "Videos";
}
