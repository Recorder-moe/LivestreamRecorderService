using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;
using Microsoft.EntityFrameworkCore;

namespace LivestreamRecorder.DB.Core
{
    public class UnitOfWork : IUnitOfWork, IDisposable
    {
        private bool _disposedValue;

        public DbContext Context { get; set; }

        public UnitOfWork(DbContext context)
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

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    Context.Dispose();
                }

                _disposedValue = true;
                Context = null!;
            }
        }

        public void Dispose()
        {
            // 請勿變更此程式碼。請將清除程式碼放入 'Dispose(bool disposing)' 方法
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
