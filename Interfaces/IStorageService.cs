namespace LivestreamRecorderService.Interfaces;

public interface IStorageService
{
    Task<bool> IsVideoFileExistsAsync(string filename, CancellationToken cancellation = default);
    Task<bool> DeleteVideoBlobAsync(string filename, CancellationToken cancellation = default);
    Task UploadPublicFileAsync(string? contentType, string pathInStorage, string filePathToUpload, CancellationToken cancellation = default);
}
