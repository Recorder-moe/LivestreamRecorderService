#if COSMOSDB
using LivestreamRecorder.DB.CosmosDB;
#elif COUCHDB
using LivestreamRecorder.DB.CouchDB;
#endif
using LivestreamRecorder.DB.Enums;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Enums;
using LivestreamRecorderService.Interfaces.Job.Uploader;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.ScopedServices;

public class VideoService
{
    private readonly ILogger<VideoService> _logger;
    private readonly IUnitOfWork _unitOfWork_Public;
    private readonly IVideoRepository _videoRepository;
    private readonly IAzureUploaderService _azureUploaderService;
    private readonly IS3UploaderService _s3UploaderService;
    private readonly ServiceOption _serviceOptions;

    public VideoService(
        ILogger<VideoService> logger,
        UnitOfWork_Public unitOfWork_Public,
        IVideoRepository videoRepository,
        IOptions<ServiceOption> serviceOptions,
        IAzureUploaderService azureUploaderService,
        IS3UploaderService s3UploaderService)
    {
        _logger = logger;
        _unitOfWork_Public = unitOfWork_Public;
        _videoRepository = videoRepository;
        _azureUploaderService = azureUploaderService;
        _s3UploaderService = s3UploaderService;
        _serviceOptions = serviceOptions.Value;
    }

    public List<Video> GetVideosByStatus(VideoStatus status)
        => _videoRepository.Where(p => p.Status == status)
                           .ToList();

    public IQueryable<Video> GetVideosBySource(string source)
        => _videoRepository.Where(p => p.Source == source);

    public async Task UpdateVideoFilename(Video video, string? filename)
    {
        await _videoRepository.ReloadEntityFromDB(video);
        video!.Filename = filename;
        await _videoRepository.AddOrUpdate(video);
        _unitOfWork_Public.Commit();
        _logger.LogDebug("Update Video {videoId} filename to {filename}", video.id, filename);
    }

    public async Task UpdateVideoStatus(Video video, VideoStatus status)
    {
        await _videoRepository.ReloadEntityFromDB(video);
        video.Status = status;
        await _videoRepository.AddOrUpdate(video);
        _unitOfWork_Public.Commit();
        _logger.LogDebug("Update Video {videoId} Status to {videostatus}", video.id, status);
    }

    public async Task UpdateVideoNote(Video video, string? Note)
    {
        await _videoRepository.ReloadEntityFromDB(video);
        video.Note = Note;
        await _videoRepository.AddOrUpdate(video);
        _unitOfWork_Public.Commit();
        _logger.LogDebug("Update Video {videoId} note", video.id);
    }

    public async Task UpdateVideoArchivedTimeAsync(Video video)
    {
        await _videoRepository.ReloadEntityFromDB(video);

        video.ArchivedTime = DateTime.UtcNow;

        await _videoRepository.AddOrUpdate(video);
        _unitOfWork_Public.Commit();
    }

    public async Task TransferVideoFromSharedVolumeToStorageAsync(Video video, CancellationToken cancellation = default)
    {
        try
        {
            await UpdateVideoStatus(video, VideoStatus.Uploading);

            string instanceName;
            switch (_serviceOptions.StorageService)
            {
                case ServiceName.AzureBlobStorage:
                    instanceName = _azureUploaderService.GetInstanceName(video.id);
                    await _azureUploaderService.InitJobAsync(url: video.id,
                                                             video: video,
                                                             cancellation: cancellation);
                    break;
                case ServiceName.S3:
                    instanceName = _s3UploaderService.GetInstanceName(video.id);
                    await _s3UploaderService.InitJobAsync(url: video.id,
                                                          video: video,
                                                          cancellation: cancellation);
                    break;
                default:
                    throw new NotSupportedException($"StorageService {_serviceOptions.StorageService} is not supported.");
            }
        }
        catch (Exception e)
        {
            await UpdateVideoStatus(video, VideoStatus.Error);
            await UpdateVideoNote(video, $"Exception happened when uploading files to storage. Please contact admin if you see this message.");
            _logger.LogError("Exception happened when uploading files to storage: {videoId}, {error}, {message}", video.id, e, e.Message);
        }
    }

    public async Task DeleteVideoAsync(Video video)
    {
        await _videoRepository.Delete(video);
        _unitOfWork_Public.Commit();
        _logger.LogDebug("Delete Video {videoId}", video.id);
    }
}
