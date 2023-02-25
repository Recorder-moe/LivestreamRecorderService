using Azure.Storage.Blobs;
using LivestreamRecorderService.DB.Models;

namespace LivestreamRecorderService.Interfaces
{
    public interface IABSService
    {
        BlobClient GetVideoBlob(Video video);

        BlobClient GetPublicBlob(string name);
    }
}