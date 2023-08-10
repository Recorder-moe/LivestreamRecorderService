using CouchDB.Driver;
using LivestreamRecorder.DB.Interfaces;

namespace LivestreamRecorder.DB.CouchDB
{
    public class UnitOfWork : IUnitOfWork, IDisposable
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

        /// <summary>
        /// There's no transaction concept in CouchDB so this actually does nothing.
        /// </summary>
        public void Commit() { }

        #region Dispose
        private bool _disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // Notice: CouchDB Context is regestered as singleton.
                    // So, we don't dispose it.
                    // https://github.com/matteobortolazzo/couchdb-net#dependency-injection
                    //Context.Dispose();
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
        #endregion
    }
}
