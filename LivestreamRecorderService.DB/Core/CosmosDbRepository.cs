using LivestreamRecorderService.DB.Exceptions;
using LivestreamRecorderService.DB.Interfaces;
using LivestreamRecorderService.DB.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Omu.ValueInjecter;
using System.Linq.Expressions;

namespace LivestreamRecorderService.DB.Core;

public abstract class CosmosDbRepository<T> : ICosmosDbRepository<T> where T : Entity
{
    protected readonly DbContext _context;

    public CosmosDbRepository(DbContext context)
    {
        context.Database.EnsureCreated();
        _context = context;
    }

    public virtual IQueryable<T> GetAll()
        => _context.Set<T>().AsQueryable();

    public virtual IQueryable<T> Where(Expression<Func<T, bool>> predicate)
        => GetAll().Where(predicate);

    public virtual async Task<T> GetByIdAsync(string id)
        => await _context.FindAsync<T>(id) ?? throw new EntityNotFoundException($"Entity with id: {id} was not found.");

    public virtual async Task<bool> ExistsAsync(string id)
        => await _context.FindAsync<T>(id) != null;

    public virtual async Task<EntityEntry<T>> AddAsync(T entity)
        => null == entity
            ? throw new ArgumentNullException(nameof(entity))
            : await ExistsAsync(entity.id)
                ? throw new EntityAlreadyExistsException($"Entity with id: {entity.id} already exists.")
                : await _context.AddAsync<T>(entity);

    public virtual async Task<EntityEntry<T>> UpdateAsync(T entity)
    {
        if (!await ExistsAsync(entity.id)) throw new EntityNotFoundException($"Entity with id: {entity.id} was not found.");

        T entityToUpdate = await GetByIdAsync(entity.id);

        entityToUpdate.InjectFrom(entity);
        return _context.Update<T>(entityToUpdate);
    }

    public virtual async Task<EntityEntry<T>> AddOrUpdateAsync(T entity)
        => await ExistsAsync(entity.id)
            ? await UpdateAsync(entity)
            : await AddAsync(entity);

    public virtual async Task<EntityEntry<T>> DeleteAsync(T entity)
    {
        if (!await ExistsAsync(entity.id)) throw new EntityNotFoundException($"Entity with id: {entity.id} was not found.");

        var entityToDelete = await GetByIdAsync(entity.id);

        return _context.Remove<T>(entityToDelete);
    }

    public virtual Task<int> SaveChangesAsync() => _context.SaveChangesAsync();

    public virtual T LoadRelatedData(T entity) => entity;

    public abstract string CollectionName { get; }
}
