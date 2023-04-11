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
    private readonly DiscordService _discordService;
    private readonly EcPayService _ecPayService;

    public CheckPendingTransactionWorker(
        ILogger<CheckPendingTransactionWorker> logger,
        IOptions<AzureOption> options,
        IServiceProvider serviceProvider,
        DiscordService discordService,
        EcPayService ecPayService)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _discordService = discordService;
        _ecPayService = ecPayService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var _ = LogContext.PushProperty("Worker", nameof(CheckPendingTransactionWorker));
        _logger.LogTrace("{Worker} starts...", nameof(CheckPendingTransactionWorker));

        int count = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            using var __ = LogContext.PushProperty("WorkerRunId", $"{nameof(CheckPendingTransactionWorker)}_{DateTime.Now:yyyyMMddHHmmssfff}");

            #region DI
            using (var scope = _serviceProvider.CreateScope())
            {
                TransactionService transactionService = scope.ServiceProvider.GetRequiredService<TransactionService>();
                #endregion

                await ProcessEcPayPendingTransactions(transactionService, stoppingToken);

                if (count % 48 == 0) // 8hr
                {
                    count = 0;
                    await ProcessAddChannelTransactions(transactionService);
                }
            }

            _logger.LogTrace("{Worker} ends. Sleep 10 minutes.", nameof(CheckPendingTransactionWorker));
            await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            count++;
        }
    }

    private async Task ProcessEcPayPendingTransactions(TransactionService transactionService, CancellationToken stoppingToken)
    {
        _logger.LogTrace("Process pending EcPay transactions");
        var transactions = transactionService.GetPendingTransactions()
                                             .Where(p => p.TokenType == TokenType.SupportToken
                                                         && p.TransactionType == TransactionType.Deposit
                                                         && p.Timestamp.AddMinutes(40) < DateTime.Now
                                                         && !string.IsNullOrEmpty(p.EcPayMerchantTradeNo))
                                             .ToList();

        if (transactions.Count > 0)
            _logger.LogTrace("Get {count} pending EcPay transactions.", transactions.Count);

        foreach (var transaction in transactions)
        {
            using var _ = LogContext.PushProperty("transactionId", transaction.id);
            var result = await _ecPayService.UpdateEcPayTradeResultAsync(transaction, stoppingToken);
            if (null != result)
            {
                transactionService.UpdateTransaction(transaction, result.Value.Item1, result.Value.Item2);
            }
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }

    private async Task ProcessAddChannelTransactions(TransactionService transactionService)
    {
        _logger.LogTrace("Process pending add channel transactions");
        var transactions = transactionService.GetPendingTransactions()
                                             .Where(p => p.TokenType == TokenType.SupportToken
                                                         && p.TransactionType == TransactionType.Withdrawal
                                                         && p.Amount == 10)
                                             .ToList();

        if (transactions.Count > 0)
            _logger.LogTrace("Get {count} pending Add channel transactions.", transactions.Count);

        foreach (var transaction in transactions)
        {
            using var _ = LogContext.PushProperty("transactionId", transaction.id);
            await _discordService.SendNewChannelMessage(transaction);
        }
    }
}
