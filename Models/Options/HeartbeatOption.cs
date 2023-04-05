namespace LivestreamRecorderService.Models.Options;

public sealed class HeartbeatOption
{
#pragma warning disable IDE1006 // 命名樣式
    public const string ConfigurationSectionName = "Heartbeat";
#pragma warning restore IDE1006 // 命名樣式

    public bool Enabled { get; set; } = false;

    public required string Endpoint { get; set; } = "";

    public int Interval { get; set; } = 300;
}

