using Azure.Storage.Blobs;
using LivestreamRecorderService.DB.Models;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices;

public class ABSService : IABSService
{
    private readonly BlobContainerClient _blobContainerClient;
    private readonly BlobContainerClient _blobContainerClient_public;

    public ABSService(
        BlobServiceClient blobServiceClient,
        IOptions<AzureOption> options)
    {
        _blobContainerClient = blobServiceClient.GetBlobContainerClient(options.Value.BlobContainerName);
        _blobContainerClient_public = blobServiceClient.GetBlobContainerClient(options.Value.BlobContainerNamePublic);
    }

    /// <summary>
    /// Get the video BlobClient with videoId in the blob container.
    /// </summary>
    /// <param name="video"></param>
    /// <returns></returns>
    public BlobClient GetVideoBlob(Video video)
        => _blobContainerClient.GetBlobClient($"videos/{video.Filename}");

    /// <summary>
    /// Get the BlobClient by name in the blob container.
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public BlobClient GetPublicBlob(string name)
        => _blobContainerClient_public.GetBlobClient(name);

}
