using LivestreamRecorderService.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace LivestreamRecorderService.Models.Options;

public class ServiceOption
{
    public const string ConfigurationSectionName = "Service";

    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter<ServiceName>))]
    public ServiceName JobService { get; set; } = ServiceName.AzureContainerInstance;

    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter<ServiceName>))]
    public ServiceName StorageService { get; set; } = ServiceName.AzureBlobStorage;

    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter<ServiceName>))]
    public ServiceName DatabaseService { get; set; } = ServiceName.AzureCosmosDB;
}
