using System.Text.Json.Serialization;

namespace LivestreamRecorderService.Models;

[method: JsonConstructor]
public readonly struct EnvironmentVariable(string name,
    string? value,
    string? secureValue)
{
    [JsonPropertyName("name")] public string Name { get; } = name;

    [JsonPropertyName("value")] public string? Value { get; } = value;

    [JsonPropertyName("secureValue")] public string? SecureValue { get; } = secureValue;
}
