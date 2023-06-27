namespace LivestreamRecorderService.Interfaces;

public interface IStorageService
{
    Task<bool> IsVideoFileExists(string? filename, CancellationToken cancellation = default);
    Task<bool> DeleteVideoBlob(string? filename, CancellationToken cancellation = default);
    Task UploadVideoFile(string? contentType, string pathInStorage, string filePathToUpload, CancellationToken cancellation = default);
}