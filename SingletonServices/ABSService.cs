using Azure.Storage.Blobs;
using LivestreamRecorderService.DB.Models;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices;

public class ABSService : IABSService
{
    private readonly BlobContainerClient _blobContainerClient;

    public ABSService(
        BlobServiceClient blobServiceClient,
        IOptions<AzureOption> options)
    {
        _blobContainerClient = blobServiceClient.GetBlobContainerClient(options.Value.BlobContainerName);
    }

    /// <summary>
    /// Get the video BlobClient with videoId in the blob container.
    /// </summary>
    /// <param name="video"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    public BlobClient GetBlobByVideo(Video video, CancellationToken cancellation = default)
        => GetBlobByName($"videos/{video.Filename}", cancellation);

    /// <summary>
    /// Get the BlobClient with videoId in the blob container.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    public BlobClient GetBlobByName(string name, CancellationToken cancellation = default)
        => _blobContainerClient.GetBlobClient(name);

}
