#if COSMOSDB
namespace LivestreamRecorder.DB.CosmosDB
{
    public class UnitOfWork_Public : UnitOfWork
    {
        public UnitOfWork_Public(PublicContext publicContext)
            : base(publicContext)
        { }
    }
}
#endif
