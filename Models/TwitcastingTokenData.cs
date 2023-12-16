using System.Text.Json.Serialization;
#nullable disable

namespace LivestreamRecorderService.Models;

public class TwitcastingTokenData
{
    [JsonPropertyName("token")]
    public string Token { get; set; }
}
