using LivestreamRecorderService.DB.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace LivestreamRecorderService.DB.Interfaces;

public interface ICosmosDbRepository<T> where T : Entity
{
    Task<EntityEntry<T>> AddAsync(T entity);
    Task<EntityEntry<T>> DeleteAsync(T entity);
    Task<T> GetByIdAsync(string id);
    Task<int> SaveChangesAsync();
    Task<T> UpdateAsync(T entity);
}
