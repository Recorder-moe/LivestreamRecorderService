using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices;

public class ABSService : IStorageService
{
    private readonly BlobContainerClient _blobContainerClient;
    private readonly BlobContainerClient _blobContainerClient_public;

    public ABSService(
        BlobServiceClient blobServiceClient,
        IOptions<AzureOption> options)
    {
        _blobContainerClient = blobServiceClient.GetBlobContainerClient(options.Value.AzuerBlobStorage!.BlobContainerName);
        _blobContainerClient_public = blobServiceClient.GetBlobContainerClient(options.Value.AzuerBlobStorage!.BlobContainerNamePublic);
    }

    public async Task<bool> IsVideoFileExists(string? filename, CancellationToken cancellation = default)
        => !string.IsNullOrEmpty(filename)
            && (await _blobContainerClient.GetBlobClient($"videos/{filename}")
                                          .ExistsAsync(cancellation)).Value;

    public async Task<bool> DeleteVideoBlob(string? filename, CancellationToken cancellation = default)
        => !string.IsNullOrEmpty(filename)
            && (await _blobContainerClient.GetBlobClient($"videos/{filename}")
                                          .DeleteIfExistsAsync(snapshotsOption: DeleteSnapshotsOption.IncludeSnapshots,
                                                               cancellationToken: cancellation)).Value;

    public async Task<BlobContentInfo> UploadVideoFile(string? contentType, string pathInStorage, string tempPath, CancellationToken cancellation = default)
        => (await _blobContainerClient_public.GetBlobClient(pathInStorage)
                                             .UploadAsync(path: tempPath,
                                                          httpHeaders: new BlobHttpHeaders { ContentType = contentType },
                                                          accessTier: AccessTier.Hot,
                                                          cancellationToken: cancellation)).Value;
}
