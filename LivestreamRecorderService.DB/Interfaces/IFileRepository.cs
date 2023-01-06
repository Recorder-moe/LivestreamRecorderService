using File = LivestreamRecorderService.DB.Models.File;

namespace LivestreamRecorderService.DB.Interfaces
{
    public interface IFileRepository : ICosmosDbRepository<File> { }
}
