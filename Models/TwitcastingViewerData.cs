using System.Text.Json.Serialization;
#nullable disable

namespace LivestreamRecorderService.Models;

public class TwitcastingViewerData
{
    [JsonPropertyName("update_interval_sec")]
    public int? UpdateIntervalSec { get; set; }

    [JsonPropertyName("movie")]
    public Movie Movie { get; set; }
}

public class Category
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }
}

public class Movie
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    [JsonPropertyName("telop")]
    public string Telop { get; set; }

    [JsonPropertyName("category")]
    public Category Category { get; set; }

    [JsonPropertyName("viewers")]
    public Viewers Viewers { get; set; }

    [JsonPropertyName("pin_message")]
    public string PinMessage { get; set; }

    [JsonPropertyName("dvr")]
    public bool? Dvr { get; set; }

    [JsonPropertyName("live")]
    public bool? Live { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("ended_at")]
    public int? EndedAt { get; set; }
}

public class Viewers
{
    [JsonPropertyName("current")]
    public int? Current { get; set; }

    [JsonPropertyName("total")]
    public int? Total { get; set; }
}
