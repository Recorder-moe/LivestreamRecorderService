namespace LivestreamRecorder.DB.Enum;

public enum ServiceName
{
    // Container
    AzureContainerInstance,
    Kubernetes,
    Docker,

    // SharedVolume
    AzureFileShare,
    NFS,
    DockerVolume,

    // Storage
    AzureBlobStorage,
    S3,
    // NFS,

    // Database
    AzureCosmosDB,
    ApacheCouchDB
}
