using Azure.ResourceManager.Resources.Models;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.Services;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService
{
    public class RecordWorker : BackgroundService
    {
        private readonly ILogger<RecordWorker> _logger;
        private readonly ACIService _aCIService;
        private readonly AzureOption _azureOption;

        public RecordWorker(ILogger<RecordWorker> logger, ACIService aCIService, IOptions<AzureOption> options)
        {
            _logger = logger;
            _aCIService = aCIService;
            _azureOption = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            string videoId = "YbkGl0zJdgw";
            _logger.LogInformation("Start to create ACI: {videoId}", videoId);
            var operation = await _aCIService.CreateAzureContainerInstanceAsync(
                template: "ACI_ytarchive.json",
                parameters: new
                {
                    containerName = new
                    {
                        value = videoId.ToLower().Replace("_", "")  // [a-z0-9]([-a-z0-9]*[a-z0-9])?
                    },
                    commandOverrideArray = new
                    {
                        value = new string[] { 
                            "/usr/bin/dumb-init", "--",
                            "/ytarchive", "--add-metadata",
                                          "--merge",
                                          "--retry-frags", "30",
                                          "--thumbnail",
                                          "--write-thumbnail",
                                          "--write-description",
                                          "-o", "%(id)s",
                                          "https://www.youtube.com/watch?v=" + videoId,
                                          "best"
                        }
                    },
                    storageAccountName = new
                    {
                        value = _azureOption.StorageAccountName
                    },
                    storageAccountKey = new
                    {
                        value = _azureOption.StorageAccountKey
                    },
                    fileshareVolumeName = new
                    {
                        value = "livestream-recorder"
                    }
                },
                deploymentName: videoId,
                cancellation: stoppingToken);
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