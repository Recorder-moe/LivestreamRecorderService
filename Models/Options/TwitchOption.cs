using System.ComponentModel.DataAnnotations;

namespace LivestreamRecorderService.Models.Options
{
    public class TwitchOption
    {
#pragma warning disable IDE1006 // 命名樣式
        public const string ConfigurationSectionName = "Twitch";
#pragma warning restore IDE1006 // 命名樣式

        [Required]
        public bool Enabled { get; set; } = false;
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }
    }
}
