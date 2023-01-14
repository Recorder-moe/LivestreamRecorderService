using LivestreamRecorderService.DB.Interfaces;
using LivestreamRecorderService.DB.Models;

namespace LivestreamRecorderService.DB.Core;

public class VideoRepository : CosmosDbRepository<Video>, IVideoRepository
{
    public VideoRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }

    public override Video LoadRelatedData(Video entity)
    {
        UnitOfWork.Context.Entry(entity)
                          .Reference(video => video.Channel)
                          .Load();
        return entity;
    }

    public override string CollectionName { get; } = "Videos";
}
