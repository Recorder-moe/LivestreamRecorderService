namespace LivestreamRecorderService.Models.OptionDiscords;

public class DiscordOption
{
#pragma warning disable IDE1006 // 命名樣式
    public const string ConfigurationSectionName = "Discord";
#pragma warning restore IDE1006 // 命名樣式

    public required string Webhook { get; set; }
    public required string WebhookWarning { get; set; }
    public required string WebhookAdmin { get; set; }
    public required string FrontEndHost { get; set; }
    public required Emotes Emotes { get; set; }
    public required Mention Mention { get; set; }
}

public class Emotes
{
    public required string RecorderMoe { get; set; }
    public required string Youtube { get; set; }
    public required string Twitch { get; set; }
    public required string Twitcasting { get; set; }
}

public class Mention
{
    public string? Deleted { get; set; }
    public string? Channel { get; set; }
    public string? Admin { get; set; }
}

