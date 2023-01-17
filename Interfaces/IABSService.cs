using Azure.Storage.Blobs;
using LivestreamRecorderService.DB.Models;

namespace LivestreamRecorderService.Interfaces
{
    public interface IABSService
    {
        BlobClient GetBlobByVideo(Video video, CancellationToken cancellation = default);

        BlobClient GetBlobByName(string name, CancellationToken cancellation = default);
    }
}