using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.Storage.Files.Shares.Models;
using LivestreamRecorderService.DB.Enum;
using LivestreamRecorderService.DB.Models;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.ScopedServices;
using LivestreamRecorderService.SingletonServices;
using Microsoft.Extensions.Options;
using Serilog.Context;

namespace LivestreamRecorderService;

public class RecordWorker : BackgroundService
{
    private readonly ILogger<RecordWorker> _logger;
    private readonly ACIYtarchiveService _aCIYtarchiveService;
    private readonly ACIYtdlpService _aCIYtdlpService;
    private readonly IAFSService _aFSService;
    private readonly IServiceProvider _serviceProvider;
    readonly Dictionary<Video, ArmOperation<ArmDeploymentResource>> _operationNotFinish = new();

    public RecordWorker(
        ILogger<RecordWorker> logger,
        ACIYtarchiveService aCIYtarchiveService,
        ACIYtdlpService aCIYtdlpService,
        IAFSService aFSService,
        IOptions<AzureOption> options,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _aCIYtarchiveService = aCIYtarchiveService;
        _aCIYtdlpService = aCIYtdlpService;
        _aFSService = aFSService;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogTrace("{Worker} starts asynchronously...", nameof(RecordWorker));
        while (!stoppingToken.IsCancellationRequested)
        {
            _ = Task.Run(async () =>
            {
                #region DI
                using var scope = _serviceProvider.CreateScope();
                var videoService = scope.ServiceProvider.GetRequiredService<VideoService>();
                #endregion

                await CreateACIStartRecord(videoService, stoppingToken);
                await CreateACIStartDownload(videoService, stoppingToken);

                await CheckACIDeployStates(videoService, stoppingToken);

                var finished = await MonitorRecordingVideos(videoService);

                foreach (var kvp in finished)
                {
                    var (video, files) = (kvp.Key, kvp.Value);
                    using var _ = LogContext.PushProperty("videoId", video.id);
                    await videoService.AddFilesToVideoAsync(video, files);
                    await videoService.TransferVideoToBlobStorageAsync(video);
                }
            }, stoppingToken).ConfigureAwait(false);
            _logger.LogTrace("{Worker} triggered. Sleep 5 minutes.", nameof(RecordWorker));
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private async Task CreateACIStartRecord(VideoService videoService, CancellationToken stoppingToken)
    {
        _logger.LogInformation("Getting videos to record");
        var videos = videoService.GetVideosByStatus(VideoStatus.WaitingToRecord);
        _logger.LogInformation("Get {count} videos to record: {videoIds}", videos.Count, string.Join(", ", videos.Select(p => p.id).ToList()));

        foreach (var video in videos)
        {
            if (_operationNotFinish.Any(p => p.Key.id == video.id))
            {
                _logger.LogInformation("ACI deplotment already requested but not finish: {videoId}", video.id);
                continue;
            }

            _logger.LogInformation("Start to create ACI: {videoId}", video.id);
            var operation = await _aCIYtarchiveService.StartInstanceAsync(video.id, stoppingToken);
            _logger.LogInformation("ACI deployment started: {videoId} ", video.id);
            _operationNotFinish.Add(video, operation);
        }
    }

    private async Task CreateACIStartDownload(VideoService videoService, CancellationToken stoppingToken)
    {
        _logger.LogInformation("Getting videos to download");
        var videos = videoService.GetVideosByStatus(VideoStatus.WaitingToDownload);
        _logger.LogInformation("Get {count} videos to download: {videoIds}", videos.Count, string.Join(", ", videos.Select(p => p.id).ToList()));

        foreach (var video in videos)
        {
            if (_operationNotFinish.Any(p => p.Key.id == video.id))
            {
                _logger.LogInformation("ACI deplotment already requested but not finish: {videoId}", video.id);
                continue;
            }

            _logger.LogInformation("Start to create ACI: {videoId}", video.id);
            var operation = await _aCIYtdlpService.StartInstanceAsync(video.id, stoppingToken);
            _logger.LogInformation("ACI deployment started: {videoId} ", video.id);
            _operationNotFinish.Add(video, operation);
        }
    }

    private async Task CheckACIDeployStates(VideoService videoService, CancellationToken stoppingToken)
    {
        for (int i = _operationNotFinish.Count - 1; i >= 0; i--)
        {
            var kvp = _operationNotFinish.ElementAt(i);
            _ = await kvp.Value.UpdateStatusAsync(stoppingToken);
            if (kvp.Value.HasCompleted)
            {
                _logger.LogInformation("ACI has been deployed: {videoId} ", kvp.Key);
                await videoService.ACIDeployedAsync(kvp.Key);
                _operationNotFinish.Remove(kvp.Key);
            }
        }
    }

    /// <summary>
    /// Check recordings status and return finished videos
    /// </summary>
    /// <param name="videoService"></param>
    /// <returns>Videos that finish recording.</returns>
    private async Task<Dictionary<Video, List<ShareFileItem>>> MonitorRecordingVideos(VideoService videoService)
    {
        var finishedRecordingVideos = new Dictionary<Video, List<ShareFileItem>>();
        var videos = videoService.GetVideosByStatus(VideoStatus.Recording)
             .Concat(videoService.GetVideosByStatus(VideoStatus.Downloading));
        foreach (var video in videos)
        {
            TimeSpan delayTime = TimeSpan.FromMinutes(5);
            var files = await _aFSService.GetShareFilesByVideoId(video.id, delayTime);

            if (files.Count > 0)
            {
                _logger.LogInformation("Video recording finish! {videoId}", video.id);
                finishedRecordingVideos.Add(video, files);
            }
        }
        return finishedRecordingVideos;
    }
}