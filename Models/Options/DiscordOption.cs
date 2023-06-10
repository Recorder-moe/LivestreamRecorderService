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
    public required string RecorderMoe { get; set; }
    public required string Youtube { get; set; }
    public required string Twitch { get; set; }
    public required string Twitcasting { get; set; }
    public required string FC2 { get; set; }
}

public class Mention
{
    public string? Deleted { get; set; }
    public string? Channel { get; set; }
    public string? Admin { get; set; }
}

