using LivestreamRecorderService.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LivestreamRecorderService.Json;

// Must read:
// https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation?pivots=dotnet-8-0
[JsonSerializable(typeof(YtdlpVideoData))]
[JsonSerializable(typeof(FC2MemberData))]
[JsonSerializable(typeof(TwitcastingStreamData))]
[JsonSourceGenerationOptions(WriteIndented = true, AllowTrailingCommas = true, ReadCommentHandling = JsonCommentHandling.Skip)]
internal partial class SourceGenerationContext : JsonSerializerContext { }