using LivestreamRecorderService.DB.Core;
using LivestreamRecorderService.DB.Interfaces;
using LivestreamRecorderService.DB.Models;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.Services;
using Microsoft.Extensions.Options;
using File = LivestreamRecorderService.DB.Models.File;

namespace LivestreamRecorderService
{
    public class RecordWorker : BackgroundService
    {
        private readonly ILogger<RecordWorker> _logger;
        private readonly ACIYtarchiveService _aCIYtarchiveService;
        private readonly IServiceProvider _serviceProvider;

        public RecordWorker(
            ILogger<RecordWorker> logger,
            ACIYtarchiveService aCIYtarchiveService,
            IOptions<AzureOption> options,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _aCIYtarchiveService = aCIYtarchiveService;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //await TestACIAsync(stoppingToken);

            await TestDBAsync();
        }

        private async Task TestACIAsync(CancellationToken stoppingToken)
        {
            string videoId = "YbkGl0zJdgw";

            _logger.LogInformation("Start to create ACI: {videoId}", videoId);
            var operation = await _aCIYtarchiveService.StartInstanceAsync(videoId, stoppingToken);
            _logger.LogInformation("{videoId} ACI deployment started", videoId);

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(10000, stoppingToken);
                _ = await operation.UpdateStatusAsync(stoppingToken);
                _logger.LogInformation("{videoId} Operation complete? {hasComplete}", videoId, operation.HasCompleted);
                if (operation.HasCompleted)
                {
                    _logger.LogInformation("Success.");
                    return;
                }
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
                id = "UCGV96w_TwvyCDusr_tmcu8A",
                ChannelName = "Nito Ch. 新兎わい",
                Source = "Youtube"
            })).Entity;

            await videoRepository.AddOrUpdateAsync(new Video()
            {
                id = "YbkGl0zJdgw",
                Source = "Youtube",
                Channel = channel1,
                ChannelId = channel1.id,
                Status = DB.Enum.VideoStatus.Archived,
                IsLiveStream = true,
                Title = @"【朝活】チャンネル登録21000人耐久！見ると元気になる朝からハイテンションVtuber♡I'm a super energetic Vtuber！【新人Vtuber/新兎わい】",
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
                Files = new List<File>()
                {
                    (await fileRepository.AddOrUpdateAsync(
                        new File()
                        {
                            id = "YbkGl0zJdgw.mp4",
                            Size = 123,
                            VideoId = "YbkGl0zJdgw",
                            Channel = channel1,
                            ChannelId = channel1.id,
                        }
                    )).Entity
                }
            });
            await publicContext.SaveChangesAsync();
            #endregion

            #region 測試關聯資料載入
            var video = videoRepository.GetAll().First();
            videoRepository.LoadRelatedData(video);

            _logger.LogInformation("File count in video {video} {fileCount}", video.Title, video.Files.Count);

            var channel = channelRepository.GetAll().First();
            channelRepository.LoadRelatedData(channel);

            _logger.LogInformation("Video count in channel {channel} {videoCount}", channel.ChannelName, channel.Videos.Count);
            #endregion
        }
    }
}