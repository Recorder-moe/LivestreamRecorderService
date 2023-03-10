using LivestreamRecorderService.DB.Enum;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace LivestreamRecorderService.DB.Models;

[Table("Videos")]
[PrimaryKey(nameof(id))]
public class Video : Entity
{
    public override required string id { get; set; }

    public required string Source { get; set; }

    public required VideoStatus Status { get; set; }

    public bool? IsLiveStream { get; set; }

    public required string Title { get; set; }

    public string? Description { get; set; }

    public required Timestamps Timestamps { get; set; }

    // My system upload timestamp
    public DateTime? ArchivedTime { get; set; }

    public string? Thumbnail { get; set; }

    public string? Filename { get; set; }

    public long? Size { get; set; }

    public VideoStatus? SourceStatus { get; set; } = VideoStatus.Unknown;

    public string? Note { get; set; }

    public required string ChannelId { get; set; }

    public required Channel Channel { get; set; }
}

public class Timestamps
{
    public DateTime? PublishedAt { get; set; }
    public DateTime? ScheduledStartTime { get; set; }
    public DateTime? ActualStartTime { get; set; }
}

