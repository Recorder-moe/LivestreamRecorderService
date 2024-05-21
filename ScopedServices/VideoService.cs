#if COSMOSDB
using LivestreamRecorder.DB.CosmosDB;
#elif COUCHDB
using LivestreamRecorder.DB.CouchDB;
#endif
using LivestreamRecorder.DB.Enums;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;

namespace LivestreamRecorderService.ScopedServices;

public class VideoService(
    ILogger<VideoService> logger,
    // ReSharper disable once SuggestBaseTypeForParameterInConstructor
    UnitOfWork_Public unitOfWorkPublic,
    IVideoRepository videoRepository)
{
#pragma warning disable CA1859 // 盡可能使用具象類型以提高效能
    private readonly IUnitOfWork _unitOfWorkPublic = unitOfWorkPublic;
#pragma warning restore CA1859 // 盡可能使用具象類型以提高效能

    public List<Video> GetVideosByStatus(VideoStatus status)
        => [.. videoRepository.Where(p => p.Status == status)];

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

        if (status == VideoStatus.Archived)
            video.ArchivedTime = DateTime.UtcNow;

        await videoRepository.AddOrUpdateAsync(video);
        _unitOfWorkPublic.Commit();
        logger.LogDebug("Update Video {videoId} Status to {videoStatus}", video.id, status);
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

        await videoRepository.AddOrUpdateAsync(video);
        _unitOfWorkPublic.Commit();
    }

    public async Task DeleteVideoAsync(Video video)
    {
        await videoRepository.DeleteAsync(video);
        _unitOfWorkPublic.Commit();
        logger.LogDebug("Delete Video {videoId}", video.id);
    }
}
