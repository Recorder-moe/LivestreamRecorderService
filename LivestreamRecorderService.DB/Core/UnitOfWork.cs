using LivestreamRecorderService.DB.Interfaces;
using LivestreamRecorderService.DB.Models;
using Microsoft.EntityFrameworkCore;

namespace LivestreamRecorderService.DB.Core
{
    public class UnitOfWork : IUnitOfWork
    {
        public DbContext Context { get; set; }

        public UnitOfWork(PublicContext context)
        {
            Context = context;
            Context.Database.EnsureCreated();
        }

        public void Commit() => Context.SaveChanges();

        public T ReloadEntityFromDB<T>(T entity) where T : Entity
        {
            Context.Entry(entity).Reload();
            return entity;
        }
    }
}
