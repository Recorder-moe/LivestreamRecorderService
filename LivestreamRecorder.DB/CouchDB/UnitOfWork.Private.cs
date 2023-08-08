#if COUCHDB
namespace LivestreamRecorder.DB.CouchDB
{
    public class UnitOfWork_Private : UnitOfWork
    {
        public UnitOfWork_Private(CouchDBContext context)
            : base(context)
        { }
    }
}
#endif
