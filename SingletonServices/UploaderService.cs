using LivestreamRecorderService.Enums;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Models;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices;

public class UploaderService(
    IOptions<ServiceOption> serviceOptions,
    IOptions<S3Option> s3Options,
    IOptions<AzureOption> azureOptions
) : IUploaderService
{
    private readonly AzureOption _azureOption = azureOptions.Value;
    private readonly S3Option _s3Option = s3Options.Value;
    private readonly ServiceOption _serviceOptions = serviceOptions.Value;

    public string Image => _serviceOptions.StorageService switch
    {
        ServiceName.AzureBlobStorage => "azure-uploader",
        ServiceName.S3 => "s3-uploader",
        _ => throw new NotImplementedException()
    };

    public string ScriptName => _serviceOptions.StorageService switch
    {
        ServiceName.AzureBlobStorage => "./azure-uploader.sh",
        ServiceName.S3 => "./s3-uploader.sh",
        _ => throw new NotImplementedException()
    };

    public List<EnvironmentVariable> GetEnvironmentVariables()
    {
        return _serviceOptions.StorageService switch
        {
            ServiceName.AzureBlobStorage =>
            [
                new EnvironmentVariable("STORAGE_ACCOUNT_NAME", _azureOption.BlobStorage!.StorageAccountName, null),
                new EnvironmentVariable("STORAGE_ACCOUNT_KEY", null, _azureOption.BlobStorage.StorageAccountKey),
                new EnvironmentVariable("CONTAINER_NAME", _azureOption.BlobStorage.BlobContainerName_Private, null),
                new EnvironmentVariable("DESTINATION_DIRECTORY", null, "/videos")
            ],
            ServiceName.S3 =>
            [
                new EnvironmentVariable("S3_ENDPOINT", $"http{(_s3Option.Secure ? "s" : "")}://{_s3Option.Endpoint}", null),
                new EnvironmentVariable("S3_ACCESS_KEY", null, _s3Option.AccessKey),
                new EnvironmentVariable("S3_SECRET_KEY", null, _s3Option.SecretKey),
                new EnvironmentVariable("DESTINATION_BUCKET", _s3Option.BucketName_Private, null),
                new EnvironmentVariable("DESTINATION_DIRECTORY", "videos", null)
            ],
            _ => throw new NotImplementedException()
        };
    }
}
