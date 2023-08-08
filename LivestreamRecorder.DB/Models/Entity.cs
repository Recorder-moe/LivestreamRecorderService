#if COUCHDB
using CouchDB.Driver.Types;
#endif
using LivestreamRecorder.DB.Interfaces;

namespace LivestreamRecorder.DB.Models;

public abstract class Entity :
#if COUCHDB
    CouchDocument,
#endif
    IEntity
{
    /// <summary>
    /// Entity identifier
    /// </summary>
    public virtual string id { get; set; } = Guid.NewGuid().ToString().Replace("-", "");

#if !COUCHDB
    public virtual string Id => id;
#endif
}