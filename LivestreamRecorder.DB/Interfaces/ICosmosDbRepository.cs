using LivestreamRecorder.DB.Models;
using LivestreamRecorder.DB.Exceptions;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Linq.Expressions;

namespace LivestreamRecorder.DB.Interfaces;

public interface ICosmosDbRepository<T> where T : Entity
{
    string CollectionName { get; }
    IUnitOfWork UnitOfWork { get; set; }

    EntityEntry<T> Add(T entity);
    EntityEntry<T> AddOrUpdate(T entity);
    IQueryable<T> All();
    EntityEntry<T> Delete(T entity);
    bool Exists(string id);

    /// <summary>
    /// Get a single entity by id. Will throw a <see cref="EntityNotFoundException"/> if no entity is found.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    /// <exception cref="EntityNotFoundException"></exception>
    T GetById(string id);
    IQueryable<T> GetByPartitionKey(string partitionKey);
    T LoadRelatedData(T entity);
    EntityEntry<T> Update(T entity);
    IQueryable<T> Where(Expression<Func<T, bool>> predicate);
}
