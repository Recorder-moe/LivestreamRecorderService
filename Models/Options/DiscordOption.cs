using System.ComponentModel.DataAnnotations;

namespace LivestreamRecorderService.Models.OptionDiscords;

public class DiscordOption
{
#pragma warning disable IDE1006 // 命名樣式
    public const string ConfigurationSectionName = "Discord";
#pragma warning restore IDE1006 // 命名樣式

    public bool Enabled { get; set; }
    public string? Webhook { get; set; }
    public string? WebhookWarning { get; set; }
    public string? WebhookAdmin { get; set; }
    public string? FrontEndHost { get; set; }
    public Emotes? Emotes { get; set; }
    public Mention? Mention { get; set; }
}

public class Emotes
{
    [Required]
    public string RecorderMoe { get; set; } = "";
    [Required]
    public string Youtube { get; set; } = "";
    [Required]
    public string Twitch { get; set; } = "";
    [Required]
    public string Twitcasting { get; set; } = "";
    [Required]
    public string FC2 { get; set; } = "";
}

public class Mention
{
    public string? Deleted { get; set; }
    public string? Channel { get; set; }
    public string? Admin { get; set; }
}

