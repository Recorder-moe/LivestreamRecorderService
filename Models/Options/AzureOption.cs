namespace LivestreamRecorderService.Models.Options;

public sealed class AzureOption
{
#pragma warning disable IDE1006 // 命名樣式
    public const string ConfigurationSectionName = "Azure";
#pragma warning restore IDE1006 // 命名樣式

    public required string ResourceGroupName { get; set; }
    public required string StorageAccountName { get; set; }
    public required string StorageAccountKey { get; set; }
}

