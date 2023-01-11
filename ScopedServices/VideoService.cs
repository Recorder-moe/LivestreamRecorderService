using Azure.Storage.Files.Shares.Models;
using LivestreamRecorderService.DB.Enum;
using LivestreamRecorderService.DB.Interfaces;
using LivestreamRecorderService.DB.Models;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.SingletonServices;
using Microsoft.Extensions.Options;
using System.Web;

namespace LivestreamRecorderService.ScopedServices;

public class VideoService
{
    private readonly ILogger<VideoService> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IVideoRepository _videoRepository;
    private readonly IFileRepository _fileRepository;
    private readonly IHttpClientFactory _httpFactory;

    public VideoService(
        ILogger<VideoService> logger,
        IUnitOfWork unitOfWork,
        IVideoRepository videoRepository,
        IFileRepository fileRepository,
        IHttpClientFactory httpFactory,
        IOptions<AzureOption> options)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _videoRepository = videoRepository;
        _fileRepository = fileRepository;
        _httpFactory = httpFactory;
    }

    public List<Video> GetVideosByStatus(VideoStatus status)
        => _videoRepository.Where(p => p.Status == status)
                           .Select(p => _videoRepository.LoadRelatedData(p))
                           .ToList();

    public void UpdateVideoStatus(Video video, VideoStatus status)
    {
        video.Status = status;
        _videoRepository.Update(video);
        _unitOfWork.Commit();
        _logger.LogDebug("Update Video {videoId} Status to {videostatus}", video.id, status);
    }

    public void AddFilesToVideo(Video video, List<ShareFileItem> sharefileItems)
    {
        video = _videoRepository.GetById(video.id);
        _videoRepository.LoadRelatedData(video);
        var files = AFSService.ConvertFileShareItemsToFilesEntities(video, sharefileItems);

        // Remove files if already exists.
        foreach (var file in files)
        {
            if (_fileRepository.Exists(file.id))
                _fileRepository.Delete(_fileRepository.GetById(file.id));
        }
        _unitOfWork.Commit();

        video.Files = files;
        video.ArchivedTime = DateTime.UtcNow;
        _videoRepository.Update(video);
        _unitOfWork.Commit();
    }

    public async Task TransferVideoToBlobStorageAsync(Video video)
    {
        var oldStatus = video.Status;
        UpdateVideoStatus(video, VideoStatus.Uploading);

        try
        {
            _logger.LogInformation("Call Azure Function to transfer video to blob storage: {videoId}", video.id);
            using var client = _httpFactory.CreateClient("AzureFileShares2BlobContainers");
            var response = await client.PostAsync("AzureFileShares2BlobContainers?videoId=" + HttpUtility.UrlEncode(video.id), null);
            response.EnsureSuccessStatusCode();

            UpdateVideoStatus(video, VideoStatus.Archived);
        }
        catch (Exception e)
        {
            UpdateVideoStatus(video, oldStatus);
            _logger.LogError("Exception happened when calling Azure Function to transfer files to blob storage: {videoId}, {error}, {message}", video.id, e, e.Message);
        }
    }

    public void RollbackVideosStatusStuckAtUploading() 
        => GetVideosByStatus(VideoStatus.Uploading)
            .Where(p => p.ArchivedTime.HasValue
                        && p.ArchivedTime.Value.AddMinutes(15) < DateTime.UtcNow)
            .ToList()
            .ForEach(p => UpdateVideoStatus(p, VideoStatus.Recording));

}
