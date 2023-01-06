using LivestreamRecorderService.DB.Interfaces;
using LivestreamRecorderService.DB.Models;

namespace LivestreamRecorderService.DB.Core;

public class VideoRepository : CosmosDbRepository<Video>, IVideoRepository
{
    public VideoRepository(PublicContext context) : base(context)
    {
    }

    public override void LoadRelatedData(Video entity)
    {
        _context.Entry(entity)
                .Collection(video => video.Files)
                .Load();
        _context.Entry(entity)
                .Reference(video => video.Channel)
                .Load();
    }

    public override string CollectionName { get; } = "Videos";
}
