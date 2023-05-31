using System.ComponentModel.DataAnnotations.Schema;

namespace LivestreamRecorder.DB.Models;
#pragma warning disable CS8618 // 退出建構函式時，不可為 Null 的欄位必須包含非 Null 值。請考慮宣告為可為 Null。

[Table("Channels")]
public class Channel : Entity
{
    public Channel()
    {
        Videos = new HashSet<Video>();
    }

    public override string id { get; set; }

    public string ChannelName { get; set; }

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

    public ICollection<Video> Videos { get; set; }
}

