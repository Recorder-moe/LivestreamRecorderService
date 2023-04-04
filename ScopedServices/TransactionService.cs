using LivestreamRecorder.DB.Core;
using LivestreamRecorder.DB.Enum;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;

namespace LivestreamRecorderService.ScopedServices;

public class TransactionService
{
    private readonly ILogger<TransactionService> _logger;
    private readonly IUnitOfWork _unitOfWork_Private;
    private readonly ITransactionRepository _transactionRepository;

    public TransactionService(
        ITransactionRepository transactionRepository,
        ILogger<TransactionService> logger,
        UnitOfWork_Private unitOfWork_Private)
    {
        _logger = logger;
        _unitOfWork_Private = unitOfWork_Private;
        _transactionRepository = transactionRepository;
    }

    internal List<Transaction> GetPendingTransactions()
        => _transactionRepository.Where(p => p.TransactionState == TransactionState.Pending)
                                 .ToList();

    public void UpdateTransaction(Transaction transaction, TransactionState status, string? TradeNo)
    {
        _unitOfWork_Private.ReloadEntityFromDB(transaction);
        transaction.TransactionState = status;
        transaction.EcPayTradeNo = TradeNo;
        _transactionRepository.Update(transaction);
        _unitOfWork_Private.Commit();
        _logger.LogDebug("Update Transaction {transactionId} to {status}", transaction.id, status);
    }
}
