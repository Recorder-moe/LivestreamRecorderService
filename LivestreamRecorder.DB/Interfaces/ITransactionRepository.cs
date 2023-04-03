using LivestreamRecorder.DB.Models;

namespace LivestreamRecorder.DB.Interfaces;

public interface ITransactionRepository : ICosmosDbRepository<Transaction>
{
}
