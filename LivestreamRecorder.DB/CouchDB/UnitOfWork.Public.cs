#if COUCHDB
namespace LivestreamRecorder.DB.CouchDB
{
    public class UnitOfWork_Public : UnitOfWork
    {
        public UnitOfWork_Public(CouchDBContext context)
            : base(context)
        { }
    }
}
#endif
