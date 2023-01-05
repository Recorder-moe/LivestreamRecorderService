using LivestreamRecorderService.DB.Enum;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace LivestreamRecorderService.DB.Models;

[Table("Videos")]
[PrimaryKey(nameof(id))]
[Index(nameof(ChannelId))]
public class Video : Entity
{
#pragma warning disable IDE1006 // 命名樣式
    public new required string id { get; set; }
#pragma warning restore IDE1006 // 命名樣式

    public required string Source { get; set; }

    public required VideoStatus Status { get; set; }

    public required bool IsLiveStream { get; set; }

    public required string Title { get; set; }

    public required string ChannelId { get; set; }
}
