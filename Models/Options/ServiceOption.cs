using LivestreamRecorder.DB.Enum;

namespace LivestreamRecorderService.Models.Options;

public class ServiceOption
{
#pragma warning disable IDE1006 // 命名樣式
    public const string ConfigurationSectionName = "Service";
#pragma warning restore IDE1006 // 命名樣式

    public required ServiceName JobService { get; set; } = ServiceName.AzureContainerInstance;
    public required ServiceName SharedVolumeService { get; set; } = ServiceName.AzureFileShare;
    public required ServiceName StorageService { get; set; } = ServiceName.AzureBlobStorage;
    public required ServiceName DatabaseService { get; set; } = ServiceName.AzureCosmosDb;
}
