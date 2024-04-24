using System.ComponentModel.DataAnnotations;

namespace LivestreamRecorderService.Models.Options;

public class TwitchOption
{
    public const string ConfigurationSectionName = "Twitch";

    [Required] public bool Enabled { get; set; } = false;
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
}
