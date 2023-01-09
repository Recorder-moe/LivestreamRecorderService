using Newtonsoft.Json;
#nullable disable

namespace LivestreamRecorderService.Models;

public class TwitcastingViewerData
{
    [JsonProperty("update_interval_sec")]
    public int? UpdateIntervalSec { get; set; }

    [JsonProperty("movie")]
    public Movie Movie { get; set; }
}

public class Category
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }
}

public class Movie
{
    [JsonProperty("id")]
    public int? Id { get; set; }

    [JsonProperty("telop")]
    public string Telop { get; set; }

    [JsonProperty("category")]
    public Category Category { get; set; }

    [JsonProperty("viewers")]
    public Viewers Viewers { get; set; }

    [JsonProperty("pin_message")]
    public string PinMessage { get; set; }

    [JsonProperty("dvr")]
    public bool? Dvr { get; set; }

    [JsonProperty("live")]
    public bool? Live { get; set; }

    [JsonProperty("title")]
    public string Title { get; set; }

    [JsonProperty("ended_at")]
    public int? EndedAt { get; set; }
}

public class Viewers
{
    [JsonProperty("current")]
    public int? Current { get; set; }

    [JsonProperty("total")]
    public int? Total { get; set; }
}
