using LivestreamRecorderService.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace LivestreamRecorderService.Models.Options;

public class ServiceOption
{
#pragma warning disable IDE1006 // 命名樣式
    public const string ConfigurationSectionName = "Service";
#pragma warning restore IDE1006 // 命名樣式

    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter<ServiceName>))]
    public ServiceName JobService { get; set; } = ServiceName.AzureContainerInstance;
    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter<ServiceName>))]
    public ServiceName SharedVolumeService { get; set; } = ServiceName.AzureFileShare;
    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter<ServiceName>))]
    public ServiceName StorageService { get; set; } = ServiceName.AzureBlobStorage;
    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter<ServiceName>))]
    public ServiceName DatabaseService { get; set; } = ServiceName.AzureCosmosDB;
}
