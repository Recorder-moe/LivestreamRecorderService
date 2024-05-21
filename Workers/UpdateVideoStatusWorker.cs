using LivestreamRecorder.DB.Enums;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Enums;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.ScopedServices;
using LivestreamRecorderService.ScopedServices.PlatformService;
using Microsoft.Extensions.Options;
using Serilog.Context;

namespace LivestreamRecorderService.Workers;

public class UpdateVideoStatusWorker(
    ILogger<UpdateVideoStatusWorker> logger,
    IStorageService storageService,
    IOptions<AzureOption> azureOptions,
    IOptions<TwitchOption> twitchOptions,
    IOptions<ServiceOption> serviceOptions,
    IOptions<S3Option> s3Options,
    IServiceProvider serviceProvider) : BackgroundService
{
    private readonly AzureOption _azureOption = azureOptions.Value;
    private readonly S3Option _s3Option = s3Options.Value;
    private readonly ServiceOption _serviceOption = serviceOptions.Value;
    private readonly TwitchOption _twitchOption = twitchOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using IDisposable _ = LogContext.PushProperty("Worker", nameof(UpdateVideoStatusWorker));
        logger.LogTrace("{Worker} starts...", nameof(UpdateVideoStatusWorker));

        var i = 0;
        DateTime expireDate = DateTime.Today;
        while (!stoppingToken.IsCancellationRequested)
        {
            using IDisposable __ = LogContext.PushProperty("WorkerRunId", $"{nameof(UpdateVideoStatusWorker)}_{DateTime.UtcNow:yyyyMMddHHmmssfff}");

            #region DI

            using (IServiceScope scope = serviceProvider.CreateScope())
            {
                VideoService videoService = scope.ServiceProvider.GetRequiredService<VideoService>();
                IVideoRepository videoRepository = scope.ServiceProvider.GetRequiredService<IVideoRepository>();
                IPlatformService youtubeService = scope.ServiceProvider.GetRequiredService<YoutubeService>();
                IPlatformService twitcastingService = scope.ServiceProvider.GetRequiredService<TwitcastingService>();
                IPlatformService fc2Service = scope.ServiceProvider.GetRequiredService<Fc2Service>();

                #endregion

                IPlatformService? twitchService = null;
                if (_twitchOption.Enabled) twitchService = scope.ServiceProvider.GetRequiredService<TwitchService>();

                // Iterate over all elements, regardless of whether their content has changed.
                i++;
                List<Video> videos =
                [
                    .. videoRepository.Where(p => p.Status >= VideoStatus.Archived
                                                  && p.Status < VideoStatus.Expired)
                                      .AsEnumerable()
                                      // Sort locally to reduce the CPU usage of CosmosDB
                                      .OrderByDescending(p => p.Timestamps.PublishedAt)
                ];

                if (videos.Count > 0)
                {
                    if (i >= videos.Count) i = 0;
                    await UpdateVideoAsync(i, videos, youtubeService, twitcastingService, fc2Service, twitchService, stoppingToken);
                }

                // Expire videos once a day
                if (DateTime.UtcNow > expireDate)
                {
                    expireDate = expireDate.AddDays(1);
                    await ExpireVideosAsync(videoService, stoppingToken);
                }
            }

            logger.LogTrace("{Worker} ends. Sleep 5 minutes.", nameof(UpdateVideoStatusWorker));
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private async Task UpdateVideoAsync(int i,
                                        List<Video> videos,
                                        IPlatformService youtubeService,
                                        IPlatformService twitcastingService,
                                        IPlatformService fc2Service,
                                        IPlatformService? twitchService,
                                        CancellationToken stoppingToken)
    {
        logger.LogInformation("Process: {index}/{amount}", i, videos.Count);

        Video video = videos[i];
        logger.LogInformation("Update video data: {videoId}", video.id);

        switch (video.Source)
        {
            case "Youtube":
                await youtubeService.UpdateVideoDataAsync(video, stoppingToken);
                break;
            case "Twitcasting":
                await twitcastingService.UpdateVideoDataAsync(video, stoppingToken);
                break;
            case "Twitch":
                if (_twitchOption.Enabled && null != twitchService)
                    await twitchService.UpdateVideoDataAsync(video, stoppingToken);

                break;
            case "FC2":
                await fc2Service.UpdateVideoDataAsync(video, stoppingToken);
                break;
            default:
                throw new InvalidOperationException($"Unknown source: {video.Source}");
        }
    }

    private async Task ExpireVideosAsync(VideoService videoService, CancellationToken cancellation)
    {
        int? retentionDays = _serviceOption.StorageService switch
        {
            ServiceName.AzureBlobStorage => _azureOption.BlobStorage?.RetentionDays,
            ServiceName.S3 => _s3Option.RetentionDays,
            _ => null
        };

        if (null == retentionDays)
        {
            logger.LogInformation("The RetentionDays setting is not configured. Videos will be skipped for expiration.");
            return;
        }

        logger.LogInformation("Start to expire videos.");
        var videos = videoService.GetVideosByStatus(VideoStatus.Archived)
                                 .Where(p => DateTime.Today - (p.ArchivedTime ?? DateTime.Today) > TimeSpan.FromDays(retentionDays.Value))
                                 .ToList();

        logger.LogInformation("Get {count} videos to expire.", videos.Count);
        foreach (Video? video in videos)
        {
            if (video.SourceStatus == VideoStatus.Deleted
                || video.SourceStatus == VideoStatus.Reject)
                logger.LogWarning("The video {videoId} that has expired does not exist on the source platform!!", video.id);

            if (!string.IsNullOrEmpty(video.Filename)
                && await storageService.DeleteVideoBlobAsync(video.Filename, cancellation))
            {
                logger.LogInformation("Delete blob {path}", video.Filename);
                await videoService.UpdateVideoStatusAsync(video, VideoStatus.Expired);
                await videoService.UpdateVideoNoteAsync(video, $"Video expired after {retentionDays} days.");
            }
            else
            {
                logger.LogError("FAILED to Delete blob {path}", video.Filename);
                await videoService.UpdateVideoStatusAsync(video, VideoStatus.Error);
                await videoService.UpdateVideoNoteAsync(video,
                                                        $"Failed to delete blob after {retentionDays} days. Please contact admin if you see this message.");
            }
        }
    }
}
