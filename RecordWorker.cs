using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.Services;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService
{
    public class RecordWorker : BackgroundService
    {
        private readonly ILogger<RecordWorker> _logger;
        private readonly ACIYtarchiveService _aCIYtarchiveService;

        public RecordWorker(
            ILogger<RecordWorker> logger,
            ACIYtarchiveService aCIYtarchiveService,
            IOptions<AzureOption> options)
        {
            _logger = logger;
            _aCIYtarchiveService = aCIYtarchiveService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
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
    }
}