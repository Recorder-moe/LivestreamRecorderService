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

public class VideoService(
    ILogger<VideoService> logger,
    // ReSharper disable once SuggestBaseTypeForParameterInConstructor
    UnitOfWork_Public unitOfWorkPublic,
    IVideoRepository videoRepository,
    IOptions<ServiceOption> serviceOptions,
    // ReSharper disable once SuggestBaseTypeForParameterInConstructor
    IAzureUploaderService azureUploaderService,
    // ReSharper disable once SuggestBaseTypeForParameterInConstructor
    IS3UploaderService s3UploaderService)
{
#pragma warning disable CA1859 // 盡可能使用具象類型以提高效能
    private readonly IUnitOfWork _unitOfWorkPublic = unitOfWorkPublic;
#pragma warning restore CA1859 // 盡可能使用具象類型以提高效能
    private readonly ServiceOption _serviceOptions = serviceOptions.Value;

    public List<Video> GetVideosByStatus(VideoStatus status)
        => videoRepository.Where(p => p.Status == status)
                          .ToList();

    public IQueryable<Video> GetVideosBySource(string source)
        => videoRepository.Where(p => p.Source == source);

    public async Task UpdateVideoFilenameAsync(Video video, string? filename)
    {
        await videoRepository.ReloadEntityFromDBAsync(video);
        video.Filename = filename;
        await videoRepository.AddOrUpdateAsync(video);
        _unitOfWorkPublic.Commit();
        logger.LogDebug("Update Video {videoId} filename to {filename}", video.id, filename);
    }

    public async Task UpdateVideoStatusAsync(Video video, VideoStatus status)
    {
        await videoRepository.ReloadEntityFromDBAsync(video);
        video.Status = status;
        await videoRepository.AddOrUpdateAsync(video);
        _unitOfWorkPublic.Commit();
        logger.LogDebug("Update Video {videoId} Status to {videostatus}", video.id, status);
    }

    public async Task UpdateVideoNoteAsync(Video video, string? note)
    {
        await videoRepository.ReloadEntityFromDBAsync(video);
        video.Note = note;
        await videoRepository.AddOrUpdateAsync(video);
        _unitOfWorkPublic.Commit();
        logger.LogDebug("Update Video {videoId} note", video.id);
    }

    public async Task UpdateVideoArchivedTimeAsync(Video video)
    {
        await videoRepository.ReloadEntityFromDBAsync(video);

        video.ArchivedTime = DateTime.UtcNow;

        await videoRepository.AddOrUpdateAsync(video);
        _unitOfWorkPublic.Commit();
    }

    public async Task TransferVideoFromSharedVolumeToStorageAsync(Video video, CancellationToken cancellation = default)
    {
        try
        {
            await UpdateVideoStatusAsync(video, VideoStatus.Uploading);

            switch (_serviceOptions.StorageService)
            {
                case ServiceName.AzureBlobStorage:
                    await azureUploaderService.InitJobAsync(url: video.id,
                        video: video,
                        cancellation: cancellation);

                    break;
                case ServiceName.S3:
                    await s3UploaderService.InitJobAsync(url: video.id,
                        video: video,
                        cancellation: cancellation);

                    break;
                default:
                    throw new NotSupportedException($"StorageService {_serviceOptions.StorageService} is not supported.");
            }
        }
        catch (Exception e)
        {
            await UpdateVideoStatusAsync(video, VideoStatus.Error);
            await UpdateVideoNoteAsync(video, "Exception happened when uploading files to storage. Please contact admin if you see this message.");
            logger.LogError(e, "Exception happened when uploading files to storage: {videoId}", video.id);
        }
    }

    public async Task DeleteVideoAsync(Video video)
    {
        await videoRepository.DeleteAsync(video);
        _unitOfWorkPublic.Commit();
        logger.LogDebug("Delete Video {videoId}", video.id);
    }
}
