using LivestreamRecorderService.DB.Enum;
using LivestreamRecorderService.DB.Interfaces;
using LivestreamRecorderService.DB.Models;

namespace LivestreamRecorderService.DB.Core;

public class VideoRepository : CosmosDbRepository<Video>, IVideoRepository
{
    public VideoRepository(PublicContext context) : base(context)
    {
    }

    public override Video LoadRelatedData(Video entity)
    {
        _context.Entry(entity)
                .Collection(video => video.Files)
                .Load();
        _context.Entry(entity)
                .Reference(video => video.Channel)
                .Load();
        return entity;
    }

    public override string CollectionName { get; } = "Videos";
}
