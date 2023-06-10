namespace LivestreamRecorder.DB.Enum;

public enum ServiceName
{
    // Container
    AzureContainerInstance,
    K8s,
    Docker,

    // PressistentVolume
    AzureFileShare,
    NFS,
    DockerVolume,

    // Storage
    AzureBlobStorage,
    S3,
    // NFS,

    // Database
    AzureCosmosDB,
    CouchDB
}
