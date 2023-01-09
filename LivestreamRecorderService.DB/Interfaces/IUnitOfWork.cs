using Microsoft.EntityFrameworkCore;

namespace LivestreamRecorderService.DB.Interfaces;

public interface IUnitOfWork
{
    DbContext Context { get; set; }

    void Commit();
}
