using LivestreamRecorder.DB.Enum;
using System.ComponentModel.DataAnnotations.Schema;

namespace LivestreamRecorder.DB.Models;
#pragma warning disable CS8618 // 退出建構函式時，不可為 Null 的欄位必須包含非 Null 值。請考慮宣告為可為 Null。

[Table("Videos")]
public class Video : Entity
{
    public override string id { get; set; }

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

    public string ChannelId { get; set; }

    public Channel? Channel { get; set; }
}

public class Timestamps
{
    public DateTime? PublishedAt { get; set; }
    public DateTime? ScheduledStartTime { get; set; }
    public DateTime? ActualStartTime { get; set; }
}

