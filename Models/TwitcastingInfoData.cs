using System.Text.Json.Serialization;
#nullable disable

namespace LivestreamRecorderService.Models;

public class TwitcastingInfoData
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    [JsonPropertyName("started_at")]
    public int? StartedAt { get; set; }

    [JsonPropertyName("visibility")]
    public Visibility Visibility { get; set; }

    [JsonPropertyName("collabo")]
    public object Collabo { get; set; }

    [JsonPropertyName("is_tool")]
    public bool? IsTool { get; set; }

    [JsonPropertyName("is_games")]
    public object IsGames { get; set; }

    [JsonPropertyName("is_vtuber")]
    public bool? IsVtuber { get; set; }

    [JsonPropertyName("is_corporate_broadcasting")]
    public object IsCorporateBroadcasting { get; set; }

    [JsonPropertyName("is_portrait")]
    public object IsPortrait { get; set; }

    [JsonPropertyName("is_dvr_supported")]
    public bool? IsDvrSupported { get; set; }
}

public class Visibility
{
    [JsonPropertyName("type")]
    public string Type { get; set; }
}
