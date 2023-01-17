using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace LivestreamRecorderService.DB.Models;

[Table("Channels")]
[PrimaryKey("id")]
public class Channel : Entity
{
    public Channel()
    {
        Videos = new HashSet<Video>();
    }

    public override required string id { get; set; }

    public required string ChannelName { get; set; }

    public required string Source { get; set; }

    public bool Monitoring { get; set; } = false;

    public string? Avatar { get; set; }

    public string? Banner { get; set; }

    public ICollection<Video> Videos { get; set; }
}

