using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;
using FileInfo = LivestreamRecorderService.Models.FileInfo;

namespace LivestreamRecorderService.SingletonServices;

public class AFSService : ISharedVolumeService
{
    private readonly ILogger<AFSService> _logger;
    private readonly ShareClient _shareClient;

    public AFSService(
        ILogger<AFSService> logger,
        ShareServiceClient shareServiceClient,
        IOptions<AzureOption> options)
    {
        _logger = logger;
        _shareClient = shareServiceClient.GetShareClient(options.Value.FileShare!.ShareName);
    }

    private async Task<ShareDirectoryClient> GetFileShareClientAsync(CancellationToken cancellation = default)
    {
        // Ensure that the share exists
        if (!await _shareClient.ExistsAsync(cancellation))
        {
            _logger.LogCritical("Share not exists: {fileShareName}!!", _shareClient.Name);
            throw new Exception("File Share does not exist.");
        }

        // Get a reference to the directory
        ShareDirectoryClient rootdirectory = _shareClient.GetRootDirectoryClient();
        return rootdirectory;
    }

    /// <summary>
    /// Search files with videoId as prefix in the file share and return when all the files matches delay filter.
    /// </summary>
    /// <param name="prefix"></param>
    /// <param name="delay"></param>
    /// <returns></returns>
    public async Task<FileInfo?> GetVideoFileInfoByPrefixAsync(string prefix, TimeSpan delay, CancellationToken cancellation = default)
    {
        ShareDirectoryClient rootDirectoryClient = await GetFileShareClientAsync(cancellation);
        List<ShareFileItem> shareFileItems =
            rootDirectoryClient
            .GetFilesAndDirectories(new ShareDirectoryGetFilesAndDirectoriesOptions()
            {
                Prefix = prefix
            }, cancellation)
            .Where(p => !p.IsDirectory)
            .ToList();

        return shareFileItems != null
                   && shareFileItems.Count > 0
                   && shareFileItems.All(p =>
                      {
                          DateTimeOffset lastModified = rootDirectoryClient.GetFileClient(p.Name).GetProperties().Value.LastModified;
                          return DateTimeOffset.Now - lastModified > delay;
                      })
               ? shareFileItems.Select(p => new FileInfo { Name = p.Name, FileSize = p.FileSize })
                               .FirstOrDefault(p => p.Name.Split('.').Last() is "mp4" or "mkv" or "webm")
               : null;
    }
}
