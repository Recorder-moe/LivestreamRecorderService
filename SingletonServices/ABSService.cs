using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices;

public class ABSService(
    BlobServiceClient blobServiceClient,
    IOptions<AzureOption> options) : IStorageService
{
    private readonly BlobContainerClient _blobContainerClient = blobServiceClient.GetBlobContainerClient(options.Value.BlobStorage!.BlobContainerName_Private);
    private readonly BlobContainerClient _blobContainerClient_public = blobServiceClient.GetBlobContainerClient(options.Value.BlobStorage!.BlobContainerName_Public);

    public async Task<bool> IsVideoFileExistsAsync(string filename, CancellationToken cancellation = default)
        => (await _blobContainerClient.GetBlobClient($"videos/{filename}")
                                      .ExistsAsync(cancellation)).Value;

    public async Task<bool> DeleteVideoBlobAsync(string filename, CancellationToken cancellation = default)
        => (await _blobContainerClient.GetBlobClient($"videos/{filename}")
                                      .DeleteIfExistsAsync(snapshotsOption: DeleteSnapshotsOption.IncludeSnapshots,
                                                           cancellationToken: cancellation)).Value;

    public Task UploadPublicFileAsync(string? contentType, string pathInStorage, string tempPath, CancellationToken cancellation = default)
        => _blobContainerClient_public.GetBlobClient(pathInStorage)
                                             .UploadAsync(path: tempPath,
                                                          httpHeaders: new BlobHttpHeaders { ContentType = contentType },
                                                          accessTier: AccessTier.Hot,
                                                          cancellationToken: cancellation);
}
