using System.Text.Json.Serialization;

namespace LivestreamRecorderService.Models;

public readonly struct EnvironmentVariable
{
    [JsonPropertyName("name")]
    public string Name { get; }

    [JsonPropertyName("value")]
    public string? Value { get; }

    [JsonPropertyName("secureValue")]
    public string? SecureValue { get; }

    [JsonConstructor]
    public EnvironmentVariable(string name, string? value, string? secureValue)
    {
        Name = name;
        Value = value;
        SecureValue = secureValue;
    }
}