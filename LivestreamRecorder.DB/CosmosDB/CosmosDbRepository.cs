using LivestreamRecorder.DB.Exceptions;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;
using Microsoft.EntityFrameworkCore;
using Omu.ValueInjecter;
using System.Linq.Expressions;

namespace LivestreamRecorder.DB.CosmosDB;

public abstract class CosmosDbRepository<T> : IRepository<T> where T : Entity
{

    private readonly DbContext _context;
    public abstract string CollectionName { get; }

    public CosmosDbRepository(IUnitOfWork unitOfWork)
    {
        UnitOfWork u = (UnitOfWork)unitOfWork;
        _context = u.Context;
    }

    private DbSet<T>? _objectset;
    private DbSet<T> ObjectSet
    {
        get
        {
            _objectset ??= _context.Set<T>();
            return _objectset;
        }
    }

    public virtual IQueryable<T> All()
        => ObjectSet.AsQueryable();

    public virtual IQueryable<T> Where(Expression<Func<T, bool>> predicate)
        => All().Where(predicate);

    public virtual Task<T?> GetById(string id)
        => All().SingleOrDefaultAsync(p => p.id == id);

    public virtual IQueryable<T> GetByPartitionKey(string partitionKey)
        => All().WithPartitionKey(partitionKey)
            ?? throw new EntityNotFoundException($"Entity with partition key: {partitionKey} was not found.");

    public virtual bool Exists(string id)
#pragma warning disable CA1827 // 不要在可使用 Any() 時使用 Count() 或 LongCount()
        => All().Where(p => p.id == id).Count() > 0;
#pragma warning restore CA1827 // 不要在可使用 Any() 時使用 Count() 或 LongCount()

    public virtual Task<T> Add(T entity)
        => null == entity
            ? throw new ArgumentNullException(nameof(entity))
            : Task.FromResult(ObjectSet.Add(entity).Entity);

    public virtual async Task<T> Update(T entity)
    {
        var entityToUpdate = await GetById(entity.id);
        if (null == entityToUpdate) throw new EntityNotFoundException($"Entity with id: {entity.id} was not found.");

        entityToUpdate.InjectFrom(entity);
        return ObjectSet.Update(entityToUpdate!).Entity;
    }

    public virtual Task<T> AddOrUpdate(T entity)
        => Exists(entity.id)
            ? Update(entity)
            : Add(entity);

    public virtual async Task Delete(T entity)
    {
        var entityToDelete = await GetById(entity.id);
        if (null == entityToDelete) throw new EntityNotFoundException($"Entity with id: {entity.id} was not found.");

        ObjectSet.Remove(entityToDelete!);
    }

    public Task<T?> ReloadEntityFromDB(T entity)
    {
        try
        {
            _context.Entry(entity).Reload();
        }
        catch (NullReferenceException) { }
#pragma warning disable CS8619 // 值中參考型別的可 Null 性與目標型別不符合。
        return Task.FromResult(entity);
#pragma warning restore CS8619 // 值中參考型別的可 Null 性與目標型別不符合。
    }
}
