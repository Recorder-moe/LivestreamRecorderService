#if COUCHDB
using CouchDB.Driver;
using CouchDB.Driver.Indexes;
using CouchDB.Driver.Options;
using LivestreamRecorder.DB.Models;

namespace LivestreamRecorder.DB.CouchDB;

public class CouchDBContext : CouchContext
{
    public CouchDBContext() { }

    public CouchDBContext(CouchOptions<CouchDBContext> options) : base(options)
    {
    }

    internal Dictionary<string, Action<IIndexBuilder<Video>>> _videoIndexes = new()
    {
        #region Used by frontend
        {
            "TimestampsPublishedAt, _id",
            (builder) => builder.IndexByDescending(p => p.Timestamps.PublishedAt)
                                .ThenByDescending(p => p.Id)
        },
        {
            "ArchivedTime, Status",
            (builder) => builder.IndexByDescending(p => p.ArchivedTime)
                                .ThenByDescending(p => p.Status)
        },
        {
            "ArchivedTime, Status, SourceStatus",
            (builder) => builder.IndexByDescending(p => p.ArchivedTime)
                                .ThenByDescending(p => p.Status)
                                .ThenByDescending(p => p.SourceStatus)
        },
        #endregion
        #region Used by service
        {
            "Status",
            (builder) => builder.IndexByDescending(p => p.Status)
        },
        {
            "Source",
            (builder) => builder.IndexByDescending(p => p.Source)
        }
        #endregion
    };

    internal Dictionary<string, Action<IIndexBuilder<Channel>>> _channelIndexes = new()
    {
        #region Used by service
        {
            "Status",
            (builder) => builder.IndexBy(p => p.Id)
                                .ThenBy(p => p.Monitoring)
        },
        {
            "Source",
            (builder) => builder.IndexByDescending(p => p.Source)
        }
        #endregion
    };

    protected override void OnDatabaseCreating(CouchDatabaseBuilder databaseBuilder)
    {
        #region Videos
        databaseBuilder.Document<Video>()
            .ToDatabase("videos");

        databaseBuilder.Document<Video>()
            .IsPartitioned();

        foreach (var index in _videoIndexes)
        {
            databaseBuilder.Document<Video>()
                .HasIndex(index.Key, index.Value, new() { Partitioned = false, });
        }
        #endregion

        #region Channels
        databaseBuilder.Document<Channel>()
            .ToDatabase("channels");

        databaseBuilder.Document<Channel>()
            .IsPartitioned();

        foreach (var index in _channelIndexes)
        {
            databaseBuilder.Document<Channel>()
                .HasIndex(index.Key, index.Value, new() { Partitioned = false, });
        }
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
