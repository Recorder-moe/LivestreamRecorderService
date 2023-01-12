using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;

namespace LivestreamRecorderService.Interfaces;

public interface IAFSService
{
    Task<ShareDirectoryClient> GetFileShareClientAsync(CancellationToken cancellation = default);
    Task<List<ShareFileItem>> GetShareFilesByVideoIdAsync(string videoId, TimeSpan delay, CancellationToken cancellation = default);
}