using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LivestreamRecorderService.DB.Models;

[Table("Files")]
[PrimaryKey("id")]
public class File : Entity
{
    public override required string id { get; set; }

    public string Directory { get; set; } = "/";

    public long? Size { get; set; }

    public required string ChannelId { get; set; }

    public string? VideoId { get; set; }

    [JsonIgnore]
    public required Channel Channel { get; set; }

    [JsonIgnore]
    public Video? Video { get; set; }
}

