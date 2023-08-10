#if COSMOSDB
using LivestreamRecorder.DB.CosmosDB;
using Microsoft.EntityFrameworkCore;
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

    public override Task<User?> GetById(string id)
#if COUCHDB
        => base.GetById($"{id}:{id}");
#elif COSMOSDB
        => base.GetByPartitionKey(id)
               .Where(p => p.id == id)
               .SingleOrDefaultAsync();
#endif

    public override string CollectionName { get; } = "Users";
}
