using Newtonsoft.Json;
#nullable disable

namespace LivestreamRecorderService.Models;

public class TwitcastingTokenData
{
    [JsonProperty("token")]
    public string Token { get; set; }
}
