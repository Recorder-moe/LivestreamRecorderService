using System.ComponentModel.DataAnnotations;

namespace LivestreamRecorderService.Models.Options;

public sealed class CouchDBOption
{
#pragma warning disable IDE1006 // 命名樣式
    public const string ConfigurationSectionName = "CouchDB";
#pragma warning restore IDE1006 // 命名樣式

    [Required]
    public string Endpoint { get; set; } = "";

    [Required]
    public string Username { get; set; } = "";

    [Required]
    public string Password { get; set; } = "";
}

