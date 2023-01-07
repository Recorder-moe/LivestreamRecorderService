using LivestreamRecorderService.DB.Models;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Linq.Expressions;

namespace LivestreamRecorderService.DB.Interfaces;

public interface ICosmosDbRepository<T> where T : Entity
{
    string CollectionName { get; }

    Task<EntityEntry<T>> AddAsync(T entity);
    Task<EntityEntry<T>> AddOrUpdateAsync(T entity);
    Task<EntityEntry<T>> DeleteAsync(T entity);
    IQueryable<T> GetAll();
    Task<T> GetByIdAsync(string id);
    Task<bool> ExistsAsync(string id);
    T LoadRelatedData(T entity);
    Task<int> SaveChangesAsync();
    Task<EntityEntry<T>> UpdateAsync(T entity);
    IQueryable<T> Where(Expression<Func<T, bool>> predicate);
}
