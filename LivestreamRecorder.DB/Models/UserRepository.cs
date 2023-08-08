#if COSMOSDB
using LivestreamRecorder.DB.CosmosDB;
#elif COUCHDB
using LivestreamRecorder.DB.CouchDB;
#endif
using LivestreamRecorder.DB.Interfaces;

namespace LivestreamRecorder.DB.Models;

public class UserRepository :
#if COSMOSDB
    CosmosDbRepository<User>,
#elif COUCHDB
    CouchDbRepository<User>,
#endif
    IUserRepository
{
    public UserRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }

    public override string CollectionName { get; } = "Users";
}
