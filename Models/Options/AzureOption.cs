namespace LivestreamRecorderService.Models.Options;

public sealed class AzureOption
{
#pragma warning disable IDE1006 // 命名樣式
    public const string ConfigurationSectionName = "Azure";
#pragma warning restore IDE1006 // 命名樣式

    public ACIOption? AzureContainerInstance { get; set; } = null;
    public AFSOption? AzureFileShare { get; set; } = null;
    public ABSOption? AzuerBlobStorage { get; set; } = null;
   }

public class ABSOption
{
    public required string StorageAccountName { get; set; }
    public required string StorageAccountKey { get; set; }
    public required string BlobContainerName { get; set; }
    public required string BlobContainerNamePublic { get; set; }
    public required int RetentionDays { get; set; }
    public string ConnectionString => $"DefaultEndpointsProtocol=https;AccountName={StorageAccountName};AccountKey={StorageAccountKey};EndpointSuffix=core.windows.net";
}

public class AFSOption
{
    public required string StorageAccountName { get; set; }
    public required string StorageAccountKey { get; set; }
    public required string ShareName { get; set; }
    public string ConnectionString => $"DefaultEndpointsProtocol=https;AccountName={StorageAccountName};AccountKey={StorageAccountKey};EndpointSuffix=core.windows.net";
}

public class ACIOption
{
    public required ClientSecretTenant ClientSecret { get; set; }
    public required string ResourceGroupName { get; set; }
}

public class ClientSecretTenant
{
    public required string TenantID { get; set; }
    public required string ClientID { get; set; }
    public required string ClientSecret { get; set; }
}