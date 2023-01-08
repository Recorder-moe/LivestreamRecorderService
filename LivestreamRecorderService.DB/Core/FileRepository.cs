using LivestreamRecorderService.DB.Interfaces;
using File = LivestreamRecorderService.DB.Models.File;

namespace LivestreamRecorderService.DB.Core;

public class FileRepository : CosmosDbRepository<File>, IFileRepository
{
    public FileRepository(PublicContext context) : base(context)
    {
    }

    public override File LoadRelatedData(File entity)
    {
        context.Entry(entity)
                .Reference(file => file.Video)
                .Load();
        context.Entry(entity)
                .Reference(file => file.Channel)
                .Load();
        return entity;
    }

    public override string CollectionName { get; } = "Files";
}
