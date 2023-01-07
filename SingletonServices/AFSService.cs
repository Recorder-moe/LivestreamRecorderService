using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using LivestreamRecorderService.DB.Models;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;
using File = LivestreamRecorderService.DB.Models.File;

namespace LivestreamRecorderService.SingletonServices
{
    public class AFSService : IAFSService
    {
        private readonly ILogger<ACIService> _logger;
        private readonly ShareClient _shareClient;

        public AFSService(
            ILogger<ACIService> logger,
            ShareServiceClient shareServiceClient,
            IOptions<AzureOption> options
        )
        {
            _logger = logger;
            _shareClient = shareServiceClient.GetShareClient(options.Value.ShareName);
        }

        public async Task<ShareDirectoryClient> GetFileShareClientAsync()
        {
            // Ensure that the share exists
            if (!await _shareClient.ExistsAsync())
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
        /// <param name="videoId"></param>
        /// <param name="delay"></param>
        /// <returns></returns>
        public async Task<List<ShareFileItem>> GetShareFilesByVideoId(string videoId, TimeSpan delay)
        {
            List<ShareFileItem> shareFileItems =
                (await GetFileShareClientAsync())
                .GetFilesAndDirectories(new ShareDirectoryGetFilesAndDirectoriesOptions()
                {
                    Prefix = videoId
                })
                .Where(p => !p.IsDirectory)
                .ToList();

            return shareFileItems != null
                       && shareFileItems.Count() > 0
                       && !shareFileItems.Any(p => Path.GetExtension(p.Name) == ".ts")
                       && shareFileItems.Any(p => Path.GetExtension(p.Name) == ".mp4")
                   ? shareFileItems
                   : new List<ShareFileItem>();
        }

        public static List<File> ConvertFileShareItemsToFilesEntities(Video video, IEnumerable<ShareFileItem> shareFileItems)
            => shareFileItems.Select(p => new File()
            {
                id = p.Name,
                ChannelId = video.ChannelId,
                Channel = video.Channel,
                Directory = "/",
                Size = p.FileSize,
                Video = video,
                VideoId = video.id
            }).ToList();
    }
}
