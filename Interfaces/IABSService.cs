using Azure.Storage.Blobs;
using LivestreamRecorderService.DB.Models;

namespace LivestreamRecorderService.Interfaces
{
    public interface IABSService
    {
        BlobClient GetBlobByVideo(Video video);

        BlobClient GetBlobByName(string name);
    }
}