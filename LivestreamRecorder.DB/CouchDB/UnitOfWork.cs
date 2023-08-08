using CouchDB.Driver;
using LivestreamRecorder.DB.Interfaces;

namespace LivestreamRecorder.DB.CouchDB
{
    public class UnitOfWork : IUnitOfWork, IAsyncDisposable
    {
        public CouchContext Context { get; set; }

        public UnitOfWork(CouchContext context)
        {
            Context = context;
            if (!Context.Client.IsUpAsync().Result)
            {
                throw new Exception($"CouchDB {Context.Client.Endpoint} is down.");
            }
        }

        public void Commit() { }

        public ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);
            return Context.DisposeAsync();
        }
    }
}
