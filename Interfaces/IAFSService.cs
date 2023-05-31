using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;

namespace LivestreamRecorderService.Interfaces;

public interface IAFSService
{
    Task<ShareDirectoryClient> GetFileShareClientAsync(CancellationToken cancellation = default);
    Task<ShareFileItem?> GetVideoShareFileByPrefixAsync(string prefix, TimeSpan delay, CancellationToken cancellation = default);
}