// ReSharper disable InconsistentNaming

namespace LivestreamRecorderService.Enums;

public enum ServiceName
{
    // Container
    AzureContainerInstance,
    Kubernetes,
    Docker,

    // SharedVolume
    AzureFileShare,
    CustomPVC,
    DockerVolume,

    // Storage
    AzureBlobStorage,
    S3,

    // Database
    AzureCosmosDB,
    ApacheCouchDB
}
