using System.ComponentModel.DataAnnotations;

namespace LivestreamRecorderService.Models.Options;

public sealed class HeartbeatOption
{
    public const string ConfigurationSectionName = "Heartbeat";

    public bool Enabled { get; set; } = false;

    [Required] public string Endpoint { get; set; } = "";

    public int Interval { get; set; } = 300;
}
