namespace LivestreamRecorderService.Models.Options;

public sealed class AzureOption
{
#pragma warning disable IDE1006 // 命名樣式
    public const string ConfigurationSectionName = "Azure";
#pragma warning restore IDE1006 // 命名樣式

    public string? ResourceGroupName { get; set; }
    public string? StorageAccountName { get; set; }
    public string? StorageAccountKey { get; set; }
    public string? ShareName { get; set; }
    public string? BlobContainerName { get; set; }
    public string? BlobContainerNamePublic { get; set; }
    public int? RetentionDays { get; set; }
    public string ConnectionString => $"DefaultEndpointsProtocol=https;AccountName={StorageAccountName};AccountKey={StorageAccountKey};EndpointSuffix=core.windows.net";
}

