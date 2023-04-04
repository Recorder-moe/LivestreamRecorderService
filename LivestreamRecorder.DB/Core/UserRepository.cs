using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;

namespace LivestreamRecorder.DB.Core;

public class UserRepository : CosmosDbRepository<User>, IUserRepository
{
    public UserRepository(UnitOfWork_Private unitOfWork) : base(unitOfWork)
    {
    }

    public override string CollectionName { get; } = "Users";
}
