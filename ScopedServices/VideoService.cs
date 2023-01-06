using Azure.Storage.Files.Shares.Models;
using LivestreamRecorderService.DB.Enum;
using LivestreamRecorderService.DB.Interfaces;
using LivestreamRecorderService.DB.Models;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.SingletonServices;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.ScopedServices;

public class VideoService
{
    private readonly ILogger<VideoService> _logger;
    private readonly IVideoRepository _videoRepository;
    private readonly IHttpClientFactory _httpFactory;
    private readonly AzureOption _azureOptions;

    public VideoService(
        ILogger<VideoService> logger,
        IVideoRepository videoRepository,
        IHttpClientFactory httpFactory,
        IOptions<AzureOption> options)
    {
        _logger = logger;
        _videoRepository = videoRepository;
        _httpFactory = httpFactory;
        _azureOptions = options.Value;
    }

    public List<Video> GetWaitingVideos()
        => _videoRepository.Where(p => p.Status == VideoStatus.WaitingToRecord).ToList();

    public List<Video> GetRecordingVideos()
        => _videoRepository.Where(p => p.Status == VideoStatus.Recording).ToList();

    public async Task UpdateVideoStatus(Video video, VideoStatus status)
    {
        var entity = await _videoRepository.GetByIdAsync(video.id);
        entity.Status = status;
        await _videoRepository.UpdateAsync(entity);
        await _videoRepository.SaveChangesAsync();
        _logger.LogDebug("Update Video Status to {videostatus}", status);
    }

    public Task ACIDeployedAsync(Video video)
        => UpdateVideoStatus(video, VideoStatus.Recording);

    public async Task AddFilesToVideoAsync(Video video, List<ShareFileItem> sharefileItems)
    {
        _videoRepository.LoadRelatedData(video);
        var files = AFSService.ConvertFileShareItemsToFilesEntities(video, sharefileItems);
        video.Files = files;
        video.ArchivedTime = DateTime.UtcNow;
        video.Timestamps.ActualEndTime = DateTime.UtcNow;
        await _videoRepository.UpdateAsync(video);
        await _videoRepository.SaveChangesAsync();
    }

    public async Task TransferVideoToBlobStorageAsync(Video video)
    {
        await UpdateVideoStatus(video, VideoStatus.Uploading);

        try
        {
            _logger.LogInformation("Call Azure Function to transfer video to blob storage: {videoId}", video.id);
            var request = new HttpRequestMessage(HttpMethod.Post, "https://azurefileshares2blobcontainers.azure-api.net/AzureFileShares2BlobContainers?videoId=" + video.id);
            request.Headers.Add("Ocp-Apim-Subscription-Key", _azureOptions.APISubscriptionKey);
            var client = _httpFactory.CreateClient();
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            await UpdateVideoStatus(video, VideoStatus.Archived);
        }
        catch (Exception e)
        {
            _logger.LogError("Exception when calling Azure Function to transfer video to blob storage: {videoId}, {error}, {message}", video.id, e, e.Message);
        }
    }
}
