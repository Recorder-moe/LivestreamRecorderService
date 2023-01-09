using LivestreamRecorderService.DB.Interfaces;
using File = LivestreamRecorderService.DB.Models.File;

namespace LivestreamRecorderService.DB.Core;

public class FileRepository : CosmosDbRepository<File>, IFileRepository
{
    public FileRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }

    public override File LoadRelatedData(File entity)
    {
        UnitOfWork.Context.Entry(entity)
                          .Reference(file => file.Video)
                          .Load();
        UnitOfWork.Context.Entry(entity)
                          .Reference(file => file.Channel)
                          .Load();
        return entity;
    }

    public override string CollectionName { get; } = "Files";
}
