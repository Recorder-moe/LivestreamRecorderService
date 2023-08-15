#if COUCHDB
using CouchDB.Driver.Types;
#endif
using LivestreamRecorder.DB.Interfaces;
using Newtonsoft.Json;

namespace LivestreamRecorder.DB.Models;

public abstract class Entity :
#if COUCHDB
    CouchDocument,
#endif
    IEntity
{
    private string? _id;

    /// <summary>
    /// Entity identifier
    /// </summary>
#pragma warning disable CA1507 // 使用 nameof 表示符號名稱
    [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
#pragma warning restore CA1507 // 使用 nameof 表示符號名稱
    public virtual string id
    {
        get
        {
            if (string.IsNullOrEmpty(_id))
            {
#if COUCHDB
                _id = base.Id.Split(':').Last();
#else
                _id = Guid.NewGuid().ToString().Replace("-", "");
#endif
            }
            return _id;
        }

        set => _id = value ?? _id;
    }

#if COUCHDB
    [JsonProperty("_id", NullValueHandling = NullValueHandling.Ignore)]
    public override string Id
    {
        get => $"{id}:{id}";
        set => id = value?.Split(':').Last() ?? "";
    }
#endif
}