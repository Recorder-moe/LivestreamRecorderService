using Azure.Storage.Blobs.Models;
using LivestreamRecorder.DB.Core;
using LivestreamRecorder.DB.Enum;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.ScopedServices;
using LivestreamRecorderService.ScopedServices.PlatformService;
using Microsoft.Extensions.Options;
using Serilog.Context;

namespace LivestreamRecorderService.Workers;

public class UpdateVideoStatusWorker : BackgroundService
{
    private readonly ILogger<UpdateVideoStatusWorker> _logger;
    private readonly IABSService _aBSService;
    private readonly AzureOption _azureOption;
    private readonly IServiceProvider _serviceProvider;

    public UpdateVideoStatusWorker(
        ILogger<UpdateVideoStatusWorker> logger,
        IABSService aBSService,
        IOptions<AzureOption> options,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _aBSService = aBSService;
        _azureOption = options.Value;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var _ = LogContext.PushProperty("Worker", nameof(UpdateVideoStatusWorker));
        _logger.LogTrace("{Worker} starts...", nameof(UpdateVideoStatusWorker));

        var i = 0;
        var videos = new List<Video>();
        var expireDate = DateTime.Today;
        while (!stoppingToken.IsCancellationRequested)
        {
            using var __ = LogContext.PushProperty("WorkerRunId", $"{nameof(UpdateVideoStatusWorker)}_{DateTime.Now:yyyyMMddHHmmssfff}");

            #region DI
            using (var scope = _serviceProvider.CreateScope())
            {
                VideoService videoService = scope.ServiceProvider.GetRequiredService<VideoService>();
                IVideoRepository videoRepository = scope.ServiceProvider.GetRequiredService<IVideoRepository>();
                IPlatformService youtubeSerivce = scope.ServiceProvider.GetRequiredService<YoutubeService>();
                IPlatformService twitcastingService = scope.ServiceProvider.GetRequiredService<TwitcastingService>();
                IPlatformService twitchService = scope.ServiceProvider.GetRequiredService<TwitchService>();
                IPlatformService fc2Service = scope.ServiceProvider.GetRequiredService<FC2Service>();
                #endregion

                videos = videoRepository.Where(p => p.Status >= VideoStatus.Archived
                                                    && p.Status < VideoStatus.Expired)
                                        .ToList()
                                        // Sort locally to reduce the CPU usage of CosmosDB
                                        .OrderByDescending(p => p.Timestamps.PublishedAt)
                                        .ToList();

                // Iterate over all elements, regardless of whether their content has changed.
                i++;
                if (i >= videos.Count) i = 0;

                _logger.LogInformation("Process: {index}/{amount}", i, videos.Count);

                var video = videos[i];
                _logger.LogInformation("Update video data: {videoId}", video.id);

                video = videoRepository.LoadRelatedData(video);

                switch (video.Source)
                {
                    case "Youtube":
                        await youtubeSerivce.UpdateVideoDataAsync(video, stoppingToken);
                        break;
                    case "Twitcasting":
                        await twitcastingService.UpdateVideoDataAsync(video, stoppingToken);
                        break;
                    case "Twitch":
                        await twitchService.UpdateVideoDataAsync(video, stoppingToken);
                        break;
                    case "FC2":
                        await fc2Service.UpdateVideoDataAsync(video, stoppingToken);
                        break;
                    default:
                        break;
                }

                if (DateTime.Now > expireDate)
                {
                    expireDate = expireDate.AddDays(1);
                    await ExpireVideosAsync(videoService, stoppingToken);
                }
            }

            _logger.LogTrace("{Worker} ends. Sleep 5 minutes.", nameof(UpdateVideoStatusWorker));
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private async Task ExpireVideosAsync(VideoService videoService, CancellationToken cancellation)
    {
        int retentionDays = _azureOption.RetentionDays;
        _logger.LogInformation("Start to expire videos.");
        var videos = videoService.GetVideosByStatus(VideoStatus.Archived)
                                 .Where(p => DateTime.Today - (p.ArchivedTime ?? DateTime.Today) > TimeSpan.FromDays(retentionDays))
                                 .ToList();
        _logger.LogInformation("Get {count} videos to expire.", videos.Count);
        foreach (var video in videos)
        {
            if(video.SourceStatus != VideoStatus.Exist)
            {
                _logger.LogInformation("Video {videoId} is not exist on source platform, keep it.", video.id);
                continue;
            }

            var blob = _aBSService.GetVideoBlob(video);
            if (await blob.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellation))
            {
                _logger.LogInformation("Delete blob {path}", blob.Name);
                videoService.UpdateVideoStatus(video, VideoStatus.Expired);
                videoService.UpdateVideoNote(video, $"Video expired after {retentionDays} days.");
            }
            else
            {
                _logger.LogError("FAILED to Delete blob {path}", blob.Name);
                videoService.UpdateVideoStatus(video, VideoStatus.Error);
                videoService.UpdateVideoNote(video, $"Failed to delete blob after {retentionDays} days. Please contact admin if you see this message.");
            }
        }
    }
}
