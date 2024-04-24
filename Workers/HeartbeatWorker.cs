using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;
using Serilog.Context;

namespace LivestreamRecorderService.Workers;

public class HeartbeatWorker(
    ILogger<HeartbeatWorker> logger,
    IHttpClientFactory httpFactory,
    IOptions<HeartbeatOption> options) : BackgroundService
{
    private readonly HeartbeatOption _option = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using IDisposable _ = LogContext.PushProperty("Worker", nameof(HeartbeatWorker));
        if (!_option.Enabled) return;

        logger.LogTrace("{Worker} starts...", nameof(HeartbeatWorker));

        while (!stoppingToken.IsCancellationRequested)
        {
            using IDisposable __ = LogContext.PushProperty("WorkerRunId", $"{nameof(HeartbeatWorker)}_{DateTime.UtcNow:yyyyMMddHHmmssfff}");

            await SendHeartbeatAsync();

            logger.LogTrace("{Worker} ends. Sleep {interval} seconds.", nameof(HeartbeatWorker), _option.Interval);
            await Task.Delay(TimeSpan.FromSeconds(_option.Interval), stoppingToken);
        }
    }

    public async Task SendHeartbeatAsync()
    {
        if (!_option.Enabled) return;

        using HttpClient client = httpFactory.CreateClient();
        try
        {
            HttpResponseMessage response = await client.GetAsync(_option.Endpoint);
            response.EnsureSuccessStatusCode();
            logger.LogTrace("Heartbeat sent successfully.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Heartbeat sent failed.");
        }
    }
}
