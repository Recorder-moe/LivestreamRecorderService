using System.ComponentModel.DataAnnotations;

namespace LivestreamRecorder.DB.Models;
#pragma warning disable CS8618 // 退出建構函式時，不可為 Null 的欄位必須包含非 Null 值。請考慮宣告為可為 Null。

public class Channel : Entity
{
#if COSMOSDB
    public Channel() : base()
    {
#pragma warning disable CS0618 // 類型或成員已經過時
        Videos = new HashSet<Video>();
#pragma warning restore CS0618 // 類型或成員已經過時
    }
#endif

#if COUCHDB
    public override string Id
    {
        get => $"{Source}:{id}";
        set
        {
            Source = value?.Split(':').First() ?? "";
            id = value?.Split(':').Last() ?? "";
        }
    }
#endif

    public string ChannelName { get; set; }

    [Required]
    public string Source { get; set; }

    public bool Monitoring { get; set; } = false;

    public string? Avatar { get; set; }

    public string? Banner { get; set; }

    public string? LatestVideoId { get; set; }

    public bool? Hide { get; set; } = false;

    public bool? UseCookiesFile { get; set; } = false;

    public bool? SkipNotLiveStream { get; set; } = true;

    public bool? AutoUpdateInfo { get; set; } = true;

    public string? Note { get; set; }

#if COSMOSDB
    [Obsolete("Relationship mapping is only supported in CosmosDB. Please avoid using it.")]
    public ICollection<Video> Videos { get; set; }
#endif
}

