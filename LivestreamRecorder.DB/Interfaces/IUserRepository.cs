using LivestreamRecorder.DB.Models;

namespace LivestreamRecorder.DB.Interfaces;

public interface IUserRepository : ICosmosDbRepository<User>
{
}
