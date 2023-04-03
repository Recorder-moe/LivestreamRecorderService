using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;

namespace LivestreamRecorder.DB.Core;

public class TransactionRepository : CosmosDbRepository<Transaction>, ITransactionRepository
{
    public TransactionRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }

    public override string CollectionName { get; } = "Transactions";
}
