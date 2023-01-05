using LivestreamRecorderService.DB.Exceptions;
using LivestreamRecorderService.DB.Interfaces;
using LivestreamRecorderService.DB.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Omu.ValueInjecter;

namespace LivestreamRecorderService.DB.Core;

public abstract class CosmosDbRepository<T> : ICosmosDbRepository<T> where T : Entity
{
    private readonly DbContext _context;

    public CosmosDbRepository(DbContext context)
    {
        context.Database.EnsureCreated();
        _context = context;
    }

    public async Task<T> GetByIdAsync(string id)
        => await _context.FindAsync<T>(id) ?? throw new EntityNotFoundException($"Entity with id: {id} was not found.");

    public Task<EntityEntry<T>> AddAsync(T entity) => _context.AddAsync<T>(entity).AsTask();

    public async Task<T> UpdateAsync(T entity)
    {
        T result = await GetByIdAsync(entity.id);

        result.InjectFrom(entity);
        _context.Update<T>(entity);
        return result;
    }

    public async Task<EntityEntry<T>> DeleteAsync(T entity)
    {
        await GetByIdAsync(entity.id);

        return _context.Remove<T>(entity);
    }

    public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();

    public abstract string CollectionName { get; }
}
