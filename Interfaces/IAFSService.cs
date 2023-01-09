using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;

namespace LivestreamRecorderService.Interfaces;

public interface IAFSService
{
    Task<ShareDirectoryClient> GetFileShareClientAsync();
    Task<List<ShareFileItem>> GetShareFilesByVideoId(string videoId, TimeSpan delay);
}