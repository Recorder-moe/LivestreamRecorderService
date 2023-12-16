using System.Text.Json.Serialization;
#nullable disable

namespace LivestreamRecorderService.Models;
public class TwitcastingStreamData
{
    [JsonPropertyName("movie")]
    public Movie Movie { get; set; }

    [JsonPropertyName("hls")]
    public Hls Hls { get; set; }

    [JsonPropertyName("fmp4")]
    public Fmp4 Fmp4 { get; set; }

    [JsonPropertyName("llfmp4")]
    public Llfmp4 Llfmp4 { get; set; }
}

public class Fmp4
{
    [JsonPropertyName("host")]
    public string Host { get; set; }

    [JsonPropertyName("proto")]
    public string Proto { get; set; }

    [JsonPropertyName("source")]
    public bool? Source { get; set; }

    [JsonPropertyName("mobilesource")]
    public bool? Mobilesource { get; set; }
}

public class Hls
{
    [JsonPropertyName("host")]
    public string Host { get; set; }

    [JsonPropertyName("proto")]
    public string Proto { get; set; }

    [JsonPropertyName("source")]
    public bool? Source { get; set; }
}

public class Llfmp4
{
    [JsonPropertyName("streams")]
    public Streams Streams { get; set; }
}

public class Streams
{
    [JsonPropertyName("main")]
    public string Main { get; set; }

    [JsonPropertyName("mobilesource")]
    public string Mobilesource { get; set; }

    [JsonPropertyName("base")]
    public string Base { get; set; }
}
