using LivestreamRecorderService.DB.Enum;
using LivestreamRecorderService.DB.Interfaces;
using LivestreamRecorderService.DB.Models;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.ScopedServices;
using Microsoft.Extensions.Options;
using Serilog.Context;

namespace LivestreamRecorderService;

public class UpdateVideoStatusWorker : BackgroundService
{
    private readonly ILogger<UpdateVideoStatusWorker> _logger;
    private readonly IServiceProvider _serviceProvider;

    public UpdateVideoStatusWorker(
        ILogger<UpdateVideoStatusWorker> logger,
        IOptions<AzureOption> options,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var _ = LogContext.PushProperty("Worker", nameof(UpdateVideoStatusWorker));
        _logger.LogTrace("{Worker} starts...", nameof(UpdateVideoStatusWorker));

        var i = 0;
        var videos = new List<Video>();
        while (!stoppingToken.IsCancellationRequested)
        {
            using var __ = LogContext.PushProperty("WorkerRunId", $"{nameof(UpdateVideoStatusWorker)}_{DateTime.Now:yyyyMMddHHmmssfff}");

            #region DI
            using (var scope = _serviceProvider.CreateScope())
            {
                VideoService videoService = scope.ServiceProvider.GetRequiredService<VideoService>();
                IVideoRepository videoRepository = scope.ServiceProvider.GetRequiredService<IVideoRepository>();
                IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                IPlatformSerivce youtubeSerivce = scope.ServiceProvider.GetRequiredService<YoutubeSerivce>();
                IPlatformSerivce twitcastingService = scope.ServiceProvider.GetRequiredService<TwitcastingService>();
                IPlatformSerivce twitchService = scope.ServiceProvider.GetRequiredService<TwitchSerivce>();
                #endregion

                videos = videoRepository.Where(p => p.Status != VideoStatus.Reject
                                                    && p.Status != VideoStatus.Expired
                                                    && p.SourceStatus != VideoStatus.Deleted)
                                        .OrderBy(p => p.Timestamps.PublishedAt)
                                        .ToList();

                // Iterate over all elements, regardless of whether their content has changed.
                i++;
                if (i >= videos.Count) i = 0;

                var video = videos[i];

                switch (video.Source)
                {
                    case "Youtube":
                        await youtubeSerivce.UpdateVideoDataAsync(video, stoppingToken);
                        videoRepository.Update(video);
                        unitOfWork.Commit();
                        break;
                    case "Twitcasting":
                        await twitcastingService.UpdateVideoDataAsync(video, stoppingToken);
                        break;
                    case "Twitch":
                        await twitchService.UpdateVideoDataAsync(video, stoppingToken);
                        break;
                    default:
                        break;
                }
            }

            _logger.LogTrace("{Worker} ends. Sleep 10 minutes.", nameof(UpdateVideoStatusWorker));
            await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
        }
    }
}
