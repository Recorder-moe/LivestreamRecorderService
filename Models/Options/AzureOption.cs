using System.ComponentModel.DataAnnotations;

// ReSharper disable InconsistentNaming

namespace LivestreamRecorderService.Models.Options;

public sealed class AzureOption
{
    public const string ConfigurationSectionName = "Azure";

    public string? AzureFileShares2BlobContainers { get; set; } = null;
    public ACIOption? ContainerInstance { get; set; } = null;
    public AFSOption? FileShare { get; set; } = null;
    public ABSOption? BlobStorage { get; set; } = null;
    public CosmosDBOptions? CosmosDB { get; set; } = null;
}

public class ABSOption
{
    [Required] public string StorageAccountName { get; set; } = "";
    [Required] public string StorageAccountKey { get; set; } = "";
    [Required] public string BlobContainerName_Private { get; set; } = "";
    [Required] public string BlobContainerName_Public { get; set; } = "";
    public int RetentionDays { get; set; } = 4;

    public string ConnectionString
        => $"DefaultEndpointsProtocol=https;AccountName={StorageAccountName};AccountKey={StorageAccountKey};EndpointSuffix=core.windows.net";
}

public class AFSOption
{
    [Required] public string StorageAccountName { get; set; } = "";
    [Required] public string StorageAccountKey { get; set; } = "";
    [Required] public string ShareName { get; set; } = "";

    public string ConnectionString
        => $"DefaultEndpointsProtocol=https;AccountName={StorageAccountName};AccountKey={StorageAccountKey};EndpointSuffix=core.windows.net";
}

public class ACIOption
{
    [Required] public ClientSecretTenant ClientSecret { get; set; } = new();
    [Required] public string ResourceGroupName { get; set; } = "";
}

public class ClientSecretTenant
{
    [Required] public string TenantID { get; set; } = "";
    [Required] public string ClientID { get; set; } = "";
    [Required] public string ClientSecret { get; set; } = "";
}

public class CosmosDBOptions
{
    [Required] public ContextInfo Public { get; set; } = new();
    [Required] public ContextInfo Private { get; set; } = new();
}

public class ContextInfo
{
    [Required] public string DatabaseName { get; set; } = "";
    [Required] public string ConnectionStrings { get; set; } = "";
}
