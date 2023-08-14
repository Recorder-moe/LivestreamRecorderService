#if COUCHDB
using CouchDB.Driver;
using CouchDB.Driver.Query.Extensions;
using LivestreamRecorder.DB.Exceptions;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;
using System.Linq.Expressions;

namespace LivestreamRecorder.DB.CouchDB;

public abstract class CouchDbRepository<T> : IRepository<T> where T : Entity
{

    public abstract string CollectionName { get; }
    private readonly CouchContext _context;

    public CouchDbRepository(IUnitOfWork unitOfWork)
    {
        UnitOfWork u = (UnitOfWork)unitOfWork;
        _context = u.Context;
    }

    private ICouchDatabase<T>? _database;
    private ICouchDatabase<T> Database
    {
        get
        {
            _database ??= _context.Client.GetDatabase<T>();
            return _database;
        }
    }

    public virtual IQueryable<T> All()
        => Database.AsQueryable();

    public virtual IQueryable<T> Where(Expression<Func<T, bool>> predicate)
        => Database.Where(predicate);

    public virtual Task<T?> GetByIdAsync(string Id)
        => Database.FindAsync(Id);

    public virtual IQueryable<T> GetByPartitionKey(string partitionKey)
        => Database.Where(p => p.Id.IsMatch(@$"^{partitionKey}:.*$"))
            ?? throw new EntityNotFoundException($"Entity with partition key: {partitionKey} was not found.");

    public virtual bool Exists(string Id)
        => Database.Any(p => p.Id == Id);

    public virtual Task<T> AddOrUpdateAsync(T entity)
        => Database.AddOrUpdateAsync(entity);

    public virtual async Task DeleteAsync(T entity)
    {
        T? entityToDelete = await GetByIdAsync(entity.Id);
        if (null == entityToDelete) throw new EntityNotFoundException($"Entity with id: {entity.Id} was not found.");

        await Database.RemoveAsync(entityToDelete);
    }

    public Task<T?> ReloadEntityFromDBAsync(T entity) => GetByIdAsync(entity.Id);
}
#endif
