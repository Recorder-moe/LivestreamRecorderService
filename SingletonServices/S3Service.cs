using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;
using Minio;
using Minio.Exceptions;

namespace LivestreamRecorderService.SingletonServices;

public class S3Service : IStorageService
{
    private readonly IMinioClient _minioClient;
    private readonly ILogger<S3Service> _logger;
    private readonly S3Option _options;

    public S3Service(
        IMinioClient minioClient,
        IOptions<S3Option> options,
        ILogger<S3Service> logger)
    {
        _minioClient = minioClient;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<bool> IsVideoFileExists(string filename, CancellationToken cancellation = default)
    {
        try
        {
            var stat = await _minioClient.StatObjectAsync(new StatObjectArgs()
                                                .WithBucket(_options.BucketName_Private)
                                                .WithObject($"videos/{filename}"), cancellation);
            return !stat.DeleteMarker;
        }
        catch (MinioException e)
        {
            _logger.LogError(e, "Failed to check video file: {filename}", filename);
            return false;
        }
    }

    public async Task<bool> DeleteVideoBlob(string filename, CancellationToken cancellation = default)
    {
        try
        {
            await _minioClient.RemoveObjectAsync(new RemoveObjectArgs()
                                .WithBucket(_options.BucketName_Private)
                                .WithObject($"videos/{filename}"), cancellation);
            return true;
        }
        catch (MinioException e)
        {
            _logger.LogError(e, "Failed to delete video file: {filename}", filename);
            return false;
        }
    }

    public async Task UploadPublicFile(string? contentType, string pathInStorage, string tempPath, CancellationToken cancellation = default)
    {
        try
        {
            var response = await _minioClient.PutObjectAsync(new PutObjectArgs()
                                                .WithBucket(_options.BucketName_Public)
                                                .WithObject(pathInStorage)
                                                .WithFileName(tempPath)
                                                .WithContentType(contentType), cancellation);
        }
        catch (MinioException e)
        {
            _logger.LogError(e, "Failed to upload public file: {filePath}", pathInStorage);
        }
    }
}
