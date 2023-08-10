using LivestreamRecorder.DB.Enums;
using System.ComponentModel.DataAnnotations;

namespace LivestreamRecorder.DB.Models;
#pragma warning disable CS8618 // 退出建構函式時，不可為 Null 的欄位必須包含非 Null 值。請考慮宣告為可為 Null。

public class Video : Entity
{
#if COUCHDB
    public override string Id
    {
        get => $"{ChannelId}:{id}";
        set
        {
            ChannelId = value?.Split(':').First() ?? "";
            id = value?.Split(':').Last() ?? "";
        }
    }
#endif

    [Required]
    public string Source { get; set; }

    public VideoStatus Status { get; set; }

    public bool? IsLiveStream { get; set; }

    public string Title { get; set; }

    public string? Description { get; set; }

    public Timestamps Timestamps { get; set; }

    // My system upload timestamp
    public DateTime? ArchivedTime { get; set; }

    public string? Thumbnail { get; set; }

    public string? Filename { get; set; }

    public long? Size { get; set; }

    public VideoStatus? SourceStatus { get; set; } = VideoStatus.Unknown;

    public string? Note { get; set; }

    [Required]
    public string ChannelId { get; set; }

#if COSMOSDB
    [Obsolete("Relationship mapping is only supported in CosmosDB. Please avoid using it.")]
    public Channel? Channel { get; set; }
#endif
}

