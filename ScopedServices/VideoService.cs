using Azure.Storage.Files.Shares.Models;
using LivestreamRecorderService.DB.Enum;
using LivestreamRecorderService.DB.Interfaces;
using LivestreamRecorderService.DB.Models;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;
using System.Web;

namespace LivestreamRecorderService.ScopedServices;

public class VideoService
{
    private readonly ILogger<VideoService> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IVideoRepository _videoRepository;
    private readonly IChannelRepository _channelRepository;
    private readonly IHttpClientFactory _httpFactory;

    public VideoService(
        ILogger<VideoService> logger,
        IUnitOfWork unitOfWork,
        IVideoRepository videoRepository,
        IChannelRepository channelRepository,
        IHttpClientFactory httpFactory,
        IOptions<AzureOption> options)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _videoRepository = videoRepository;
        _channelRepository = channelRepository;
        _httpFactory = httpFactory;
    }

    public List<Video> GetVideosByStatus(VideoStatus status)
        => _videoRepository.Where(p => p.Status == status)
                           .Select(p => _videoRepository.LoadRelatedData(p))
                           .ToList();

    public void UpdateVideoStatus(Video video, VideoStatus status)
    {
        _unitOfWork.ReloadEntityFromDB(video);
        video.Status = status;
        _videoRepository.Update(video);
        _unitOfWork.Commit();
        _logger.LogDebug("Update Video {videoId} Status to {videostatus}", video.id, status);
    }

    public void AddFilePropertiesToVideo(Video video, List<ShareFileItem> sharefileItems)
    {
        video = _videoRepository.GetById(video.id);
        _videoRepository.LoadRelatedData(video);

        var videoFile = sharefileItems.FirstOrDefault(p => p.Name.Split('.').Last() is "mp4" or "mkv" or "webm");
        if (null != videoFile)
        {
            video.Size = videoFile.FileSize;
            video.Filename = videoFile.Name;
        }
        else
        {
            _logger.LogWarning("No video file found for video {videoId}", video.id);
        }

        var thumbnail = sharefileItems.FirstOrDefault(p => p.Name.Split('.').Last() is "webp" or "jpg" or "jpeg" or "png");
        if (null != thumbnail)
        {
            video.Thumbnail = thumbnail.Name;
        }

        video.ArchivedTime = DateTime.UtcNow;
        _videoRepository.Update(video);
        _unitOfWork.Commit();
    }

    public async Task TransferVideoToBlobStorageAsync(Video video, CancellationToken cancellation = default)
    {
        try
        {
            UpdateVideoStatus(video, VideoStatus.Uploading);

            _logger.LogInformation("Call Azure Function to transfer video to blob storage: {videoId}", video.id);
            using var client = _httpFactory.CreateClient("AzureFileShares2BlobContainers");
            var response = await client.PostAsync("AzureFileShares2BlobContainers?videoId=" + HttpUtility.UrlEncode(video.id), null, cancellation);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Video {videoId} is uploaded to Azure Storage.", video.id);
            UpdateVideoStatus(video, VideoStatus.Archived);
        }
        catch (Exception e)
        {
            UpdateVideoStatus(video, VideoStatus.Error);
            _logger.LogError("Exception happened when calling Azure Function to transfer files to blob storage: {videoId}, {error}, {message}", video.id, e, e.Message);
        }
    }

    public void RollbackVideosStatusStuckAtUploading()
        => GetVideosByStatus(VideoStatus.Uploading)
            .Where(p => p.ArchivedTime.HasValue
                        && p.ArchivedTime.Value.AddMinutes(15) < DateTime.UtcNow)
            .ToList()
            .ForEach(p => UpdateVideoStatus(p, VideoStatus.Recording));

    public void UpdateChannelLatestVideo(Video video)
    {
        var channel = _channelRepository.GetById(video.ChannelId);
        channel.LatestVideoId = video.id;
        _channelRepository.Update(channel);
        _unitOfWork.Commit();
    }
}
