using LivestreamRecorderService.DB.Interfaces;
using LivestreamRecorderService.DB.Models;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.Services;
using Microsoft.Extensions.Options;

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
            
            TestDB();
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

        public void TestDB()
        {
            using var scope = _serviceProvider.CreateScope();
            var videoRepository = scope.ServiceProvider.GetRequiredService<IVideoRepository>();

            videoRepository.AddAsync(new Video()
            {
                id = "YbkGl0zJdgw",
                Source = "Youtube",
                ChannelId = "UCGV96w_TwvyCDusr_tmcu8A",
                Status = DB.Enum.VideoStatus.Archived,
                IsLiveStream = true,
                Title = @"【朝活】チャンネル登録21000人耐久！見ると元気になる朝からハイテンションVtuber♡I'm a super energetic Vtuber！【新人Vtuber/新兎わい】"
            });
            videoRepository.SaveChangesAsync();
        }
    }
}