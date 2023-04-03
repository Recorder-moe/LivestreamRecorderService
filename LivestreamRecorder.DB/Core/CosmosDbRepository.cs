using LivestreamRecorder.DB.Exceptions;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Omu.ValueInjecter;
using System.Linq.Expressions;

namespace LivestreamRecorder.DB.Core;

public abstract class CosmosDbRepository<T> : ICosmosDbRepository<T> where T : Entity
{

    public IUnitOfWork UnitOfWork { get; set; }
    public abstract string CollectionName { get; }


    public CosmosDbRepository(IUnitOfWork unitOfWork)
    {
        UnitOfWork = unitOfWork;
    }

    private DbSet<T>? _objectset;
    private DbSet<T> ObjectSet
    {
        get
        {
            _objectset ??= UnitOfWork.Context.Set<T>();
            return _objectset;
        }
    }

    public virtual IQueryable<T> All()
        => ObjectSet.AsQueryable();

    public virtual IQueryable<T> Where(Expression<Func<T, bool>> predicate)
        => All().Where(predicate);

    public virtual T GetById(string id)
        => All().SingleOrDefault(p => p.id == id)
            ?? throw new EntityNotFoundException($"Entity with id: {id} was not found.");

    public virtual bool Exists(string id)
#pragma warning disable CA1827 // 不要在可使用 Any() 時使用 Count() 或 LongCount()
        => All().Where(p => p.id == id).Count() > 0;
#pragma warning restore CA1827 // 不要在可使用 Any() 時使用 Count() 或 LongCount()

    public virtual EntityEntry<T> Add(T entity)
        => null == entity
            ? throw new ArgumentNullException(nameof(entity))
            : Exists(entity.id)
                ? throw new EntityAlreadyExistsException($"Entity with id: {entity.id} already exists.")
                : ObjectSet.Add(entity);

    public virtual EntityEntry<T> Update(T entity)
    {
        if (!Exists(entity.id)) throw new EntityNotFoundException($"Entity with id: {entity.id} was not found.");

        T entityToUpdate = GetById(entity.id);

        entityToUpdate.InjectFrom(entity);
        return ObjectSet.Update(entityToUpdate);
    }

    public virtual EntityEntry<T> AddOrUpdate(T entity)
        => Exists(entity.id)
            ? Update(entity)
            : Add(entity);

    public virtual EntityEntry<T> Delete(T entity)
    {
        if (!Exists(entity.id)) throw new EntityNotFoundException($"Entity with id: {entity.id} was not found.");

        var entityToDelete = GetById(entity.id);

        return ObjectSet.Remove(entityToDelete);
    }

    public T LoadRelatedData(T entity)
    {
        EntityEntry<T> entityEntry = UnitOfWork.Context.Entry(entity);
        entityEntry.Navigations.ToList().ForEach(p => p.Load());
        return entityEntry.Entity;
    }
}
