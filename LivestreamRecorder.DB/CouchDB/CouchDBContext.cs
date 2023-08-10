#if COUCHDB
using CouchDB.Driver;
using CouchDB.Driver.Options;
using LivestreamRecorder.DB.Models;

namespace LivestreamRecorder.DB.CouchDB;

public class CouchDBContext : CouchContext
{
    public CouchDBContext() { }

    public CouchDBContext(CouchOptions<CouchDBContext> options) : base(options)
    {
    }

    protected override void OnDatabaseCreating(CouchDatabaseBuilder databaseBuilder)
    {
        #region Videos
        databaseBuilder.Document<Video>()
            .ToDatabase("videos");

        databaseBuilder.Document<Video>()
            .IsPartitioned();
        #endregion

        #region Channels
        databaseBuilder.Document<Channel>()
            .ToDatabase("channels");

        databaseBuilder.Document<Channel>()
            .IsPartitioned();
        #endregion

        #region Users
        databaseBuilder.Document<User>()
            .ToDatabase("users");

        databaseBuilder.Document<User>()
            .IsPartitioned();
        #endregion
    }
}
#endif
