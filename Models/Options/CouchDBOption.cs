namespace LivestreamRecorderService.Models.Options;

public sealed class CouchDBOption
{
#pragma warning disable IDE1006 // 命名樣式
    public const string ConfigurationSectionName = "CouchDB";
#pragma warning restore IDE1006 // 命名樣式

    public required string Endpoint { get; set; } = "";

    public required string Username { get; set; } = "";

    public required string Password { get; set; } = "";
}

