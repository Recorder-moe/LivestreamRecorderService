#if COSMOSDB
namespace LivestreamRecorder.DB.CosmosDB
{
    public class UnitOfWork_Private : UnitOfWork
    {
        public UnitOfWork_Private(PrivateContext privateContext)
            : base(privateContext)
        { }
    }
}
#endif
