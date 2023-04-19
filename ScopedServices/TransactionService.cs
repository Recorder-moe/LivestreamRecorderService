using LivestreamRecorder.DB.Core;
using LivestreamRecorder.DB.Enum;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderBackend.Helper;

namespace LivestreamRecorderService.ScopedServices;

public class TransactionService
{
    private readonly ILogger<TransactionService> _logger;
    private readonly IUnitOfWork _unitOfWork_Private;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IUserRepository _userRepository;

    public TransactionService(
        ITransactionRepository transactionRepository,
        IUserRepository userRepository,
        ILogger<TransactionService> logger,
        UnitOfWork_Private unitOfWork_Private)
    {
        _logger = logger;
        _unitOfWork_Private = unitOfWork_Private;
        _transactionRepository = transactionRepository;
        _userRepository = userRepository;
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

    private Transaction InitNewTransaction(string userId, TokenType tokenType, TransactionType transactionType, decimal amount, string? channelId = null, string? videoId = null)
    {
        var transaction = new Transaction()
        {
            id = Guid.NewGuid().ToString(),
            TokenType = tokenType,
            UserId = userId,
            TransactionType = transactionType,
            Amount = amount,
            Timestamp = DateTime.UtcNow,
            TransactionState = TransactionState.Pending,
            ChannelId = channelId,
            VideoId = videoId
        };

        // Prevent GUID conflicts
        if (_transactionRepository.Exists(transaction.id)) transaction.id = Guid.NewGuid().ToString();

        var entry = _transactionRepository.Add(transaction);
        _unitOfWork_Private.Commit();
        _logger.LogInformation("Init new transaction {TransactionId} for user {UserId}", transaction.id, userId);
        return entry.Entity;
    }

    /// <summary>
    /// 推薦計劃獎勵推薦者
    /// </summary>
    /// <param name="transaction"></param>
    public void RewardReferrer(Transaction transaction)
    {
        transaction = _transactionRepository.LoadRelatedData(transaction);
        if (null != transaction.User.Referral?.Code)
        {
            var referrerId = AESHelper.GetReferrerIdFromReferee(transaction.User);
            if (!string.IsNullOrEmpty(referrerId))
            {
                var reward = Math.Floor(transaction.Amount * 0.1M);
                var referrer = _userRepository.GetById(referrerId);
                if (referrer != null)
                {
                    var referrerTransaction = InitNewTransaction(userId: referrer.id,
                                                                 tokenType: TokenType.SupportToken,
                                                                 transactionType: TransactionType.Deposit,
                                                                 amount: reward);
                    referrerTransaction.Note = $"Referral program rewards {reward} ST: Referee purchased {transaction.Amount} ST, transaction ID {transaction.id}";
                    referrerTransaction.TransactionState = TransactionState.Success;
                    _transactionRepository.Update(referrerTransaction);

                    referrer.Tokens.SupportToken += reward;
                    _userRepository.Update(referrer);
                }
            }
        }
    }
}
