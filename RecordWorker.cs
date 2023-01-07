using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.Storage.Files.Shares.Models;
using LivestreamRecorderService.DB.Core;
using LivestreamRecorderService.DB.Interfaces;
using LivestreamRecorderService.DB.Models;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.ScopedServices;
using LivestreamRecorderService.SingletonServices;
using Microsoft.Extensions.Options;
using File = LivestreamRecorderService.DB.Models.File;

namespace LivestreamRecorderService
{
    public class RecordWorker : BackgroundService
    {
        private readonly ILogger<RecordWorker> _logger;
        private readonly ACIYtarchiveService _aCIYtarchiveService;
        private readonly IAFSService _aFSService;
        private readonly IServiceProvider _serviceProvider;
        readonly Dictionary<Video, ArmOperation<ArmDeploymentResource>> _operationNotFinish = new();

        public RecordWorker(
            ILogger<RecordWorker> logger,
            ACIYtarchiveService aCIYtarchiveService,
            IAFSService aFSService,
            IOptions<AzureOption> options,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _aCIYtarchiveService = aCIYtarchiveService;
            _aFSService = aFSService;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await TestDBAsync();
            while (!stoppingToken.IsCancellationRequested)
            {
                _ = Task.Run(async () =>
                {
                    #region DI
                    using var scope = _serviceProvider.CreateScope();
                    var videoService = scope.ServiceProvider.GetRequiredService<VideoService>();
                    #endregion

                    await CreateACIStartRecord(videoService, stoppingToken);

                    await CheckACIDeployStates(videoService, stoppingToken);

                    var finished = await MonitorRecordingVideos(videoService);

                    foreach (var kvp in finished)
                    {
                        var (video, files) = (kvp.Key, kvp.Value);
                        await videoService.AddFilesToVideoAsync(video, files);
                        await videoService.TransferVideoToBlobStorageAsync(video);
                    }
                }, stoppingToken).ConfigureAwait(false);
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
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
            foreach (var video in videoService.GetRecordingVideos())
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

        private async Task CreateACIStartRecord(VideoService videoService, CancellationToken stoppingToken)
        {
            _logger.LogInformation("Getting videos to record");
            var videos = videoService.GetWaitingVideos();
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

        public async Task TestDBAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var videoRepository = scope.ServiceProvider.GetRequiredService<IVideoRepository>();
            var channelRepository = scope.ServiceProvider.GetRequiredService<IChannelRepository>();
            var fileRepository = scope.ServiceProvider.GetRequiredService<IFileRepository>();
            var publicContext = scope.ServiceProvider.GetRequiredService<PublicContext>();

            #region 建立資料
            var channel1 = (await channelRepository.AddOrUpdateAsync(new Channel()
            {
                id = "UCiLt4FLjMXszLOh5ISi1oqw",
                ChannelName = "Kakeru Ch. 間取かける",
                Source = "Youtube"
            })).Entity;

            await videoRepository.AddOrUpdateAsync(new Video()
            {
                id = "F9NM5zVxZBU",
                Source = "Youtube",
                Channel = channel1,
                ChannelId = channel1.id,
                Status = DB.Enum.VideoStatus.Recording,
                IsLiveStream = true,
                Title = @"【歌枠/sing songs】金曜夜、夜遅めにま～っとり歌いましょ【#Vtuber/#間取かける】",
                ArchivedTime = DateTime.Now,
                Description = "",
                Duration = 123,
                Timestamps = new Timestamps()
                {
                    ActualStartTime = DateTime.Now,
                    ActualEndTime = DateTime.Now,
                    PublishedAt = DateTime.Today,
                    ScheduledStartTime = DateTime.Now
                },
                Files = new List<File>() { }
            });
            await publicContext.SaveChangesAsync();
            #endregion

            //#region 測試關聯資料載入
            //var video = videoRepository.GetAll().First();
            //_ = videoRepository.LoadRelatedData(video);

            //_logger.LogInformation("File count in video {video} {fileCount}", video.Title, video.Files.Count);

            //var channel = channelRepository.GetAll().First();
            //channelRepository.LoadRelatedData(channel);

            //_logger.LogInformation("Video count in channel {channel} {videoCount}", channel.ChannelName, channel.Videos.Count);
            //#endregion
        }
    }
}