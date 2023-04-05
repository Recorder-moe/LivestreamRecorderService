using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;
using Serilog.Context;

namespace LivestreamRecorderService.Workers;

public class HeartbeatWorker : BackgroundService
{
    private readonly ILogger<HeartbeatWorker> _logger;
    private readonly IHttpClientFactory _httpFactory;
    private readonly HeartbeatOption _option;

    public HeartbeatWorker(
        ILogger<HeartbeatWorker> logger,
        IHttpClientFactory httpFactory,
        IOptions<HeartbeatOption> options)
    {
        _logger = logger;
        _httpFactory = httpFactory;
        _option = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var _ = LogContext.PushProperty("Worker", nameof(HeartbeatWorker));
        if (!_option.Enabled) return;

        _logger.LogTrace("{Worker} starts...", nameof(HeartbeatWorker));

        while (!stoppingToken.IsCancellationRequested)
        {
            using var __ = LogContext.PushProperty("WorkerRunId", $"{nameof(HeartbeatWorker)}_{DateTime.Now:yyyyMMddHHmmssfff}");

            await SendHeartbeatAsync();

            _logger.LogTrace("{Worker} ends. Sleep {interval} seconds.", nameof(HeartbeatWorker), _option.Interval);
            await Task.Delay(TimeSpan.FromSeconds(_option.Interval), stoppingToken);
        }
    }

    public async Task SendHeartbeatAsync()
    {
        if (!_option.Enabled) return;

        var client = _httpFactory.CreateClient();
        var response = await client.GetAsync(_option.Endpoint);
        if (response.IsSuccessStatusCode)
        {
            _logger.LogTrace("Heartbeat sent successfully.");
        }
        else
        {
            _logger.LogError("Heartbeat sent failed.");
        }
    }

}
