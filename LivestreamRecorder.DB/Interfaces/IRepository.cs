﻿using LivestreamRecorder.DB.Exceptions;
using System.Linq.Expressions;

namespace LivestreamRecorder.DB.Interfaces;

public interface IRepository<T> where T : IEntity
{
    string CollectionName { get; }

    Task<T> AddOrUpdate(T entity);
    IQueryable<T> All();
    Task Delete(T entity);
    bool Exists(string id);

    /// <summary>
    /// Get a single entity by id. Will throw a <see cref="EntityNotFoundException"/> if no entity is found.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    /// <exception cref="EntityNotFoundException"></exception>
    Task<T?> GetById(string id);
    IQueryable<T> GetByPartitionKey(string partitionKey);
    T LoadRelatedData(T entity);
    Task<T?> ReloadEntityFromDB(T entity);
    IQueryable<T> Where(Expression<Func<T, bool>> predicate);
}