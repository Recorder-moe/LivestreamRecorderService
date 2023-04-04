using LivestreamRecorder.DB.Enum;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.ScopedServices;
using LivestreamRecorderService.SingletonServices;
using Microsoft.Extensions.Options;
using Serilog.Context;

namespace LivestreamRecorderService.Workers;

public class CheckPendingTransactionWorker : BackgroundService
{
    private readonly ILogger<CheckPendingTransactionWorker> _logger;
    private readonly IServiceProvider _serviceProvider;

    public CheckPendingTransactionWorker(
        ILogger<CheckPendingTransactionWorker> logger,
        IOptions<AzureOption> options,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var _ = LogContext.PushProperty("Worker", nameof(CheckPendingTransactionWorker));
        _logger.LogTrace("{Worker} starts...", nameof(CheckPendingTransactionWorker));

        while (!stoppingToken.IsCancellationRequested)
        {
            using var __ = LogContext.PushProperty("WorkerRunId", $"{nameof(CheckPendingTransactionWorker)}_{DateTime.Now:yyyyMMddHHmmssfff}");

            #region DI
            using (var scope = _serviceProvider.CreateScope())
            {
                TransactionService transactionService = scope.ServiceProvider.GetRequiredService<TransactionService>();
                EcPayService ecPayService = scope.ServiceProvider.GetRequiredService<EcPayService>();
                #endregion

                var transactions = transactionService.GetPendingTransactions()
                                                     .Where(p => p.TokenType == TokenType.SupportToken
                                                                 && p.TransactionType == TransactionType.Deposit
                                                                 && p.Timestamp.AddMinutes(40) < DateTime.Now
                                                                 && !string.IsNullOrEmpty(p.EcPayMerchantTradeNo))
                                                     .ToList();

                if (transactions.Count > 0)
                    _logger.LogTrace("Get {count} pending transactions.", transactions.Count);

                foreach (var transaction in transactions)
                {
                    using var ___ = LogContext.PushProperty("transactionId", transaction.id);
                    var result = await ecPayService.UpdateEcPayTradeResultAsync(transaction, stoppingToken);
                    if (null != result)
                    {
                        transactionService.UpdateTransaction(transaction, result.Value.Item1, result.Value.Item2);
                    }
                    await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                }
            }

            _logger.LogTrace("{Worker} ends. Sleep 10 minutes.", nameof(CheckPendingTransactionWorker));
            await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
        }
    }
}
