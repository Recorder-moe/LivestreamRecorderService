using System.ComponentModel.DataAnnotations;

namespace LivestreamRecorderService.Models.Options;

public sealed class CouchDbOption
{
    public const string ConfigurationSectionName = "CouchDB";

    [Required] public string Endpoint { get; set; } = "";

    [Required] public string Username { get; set; } = "";

    [Required] public string Password { get; set; } = "";
}
