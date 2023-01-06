using Azure.Storage.Files.Shares.Models;
using LivestreamRecorderService.DB.Interfaces;
using LivestreamRecorderService.DB.Models;
using File = LivestreamRecorderService.DB.Models.File;

namespace LivestreamRecorderService.ScopedServices;

public class FileService
{
    private readonly IFileRepository _fileRepository;

    public FileService(
        IFileRepository fileRepository
    )
    {
        _fileRepository = fileRepository;
    }
}
