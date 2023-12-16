using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;
using Minio;
using Minio.Exceptions;

namespace LivestreamRecorderService.SingletonServices;

public class S3Service(
    IMinioClient minioClient,
    IOptions<S3Option> options,
    ILogger<S3Service> logger) : IStorageService
{
    private readonly S3Option _options = options.Value;

    public async Task<bool> IsVideoFileExistsAsync(string filename, CancellationToken cancellation = default)
    {
        try
        {
            var stat = await minioClient.StatObjectAsync(new StatObjectArgs()
                                         .WithBucket(_options.BucketName_Private)
                                         .WithObject($"videos/{filename}"), cancellation);
            return !stat.DeleteMarker;
        }
        catch (MinioException e)
        {
            if (e is ObjectNotFoundException)
            {
                logger.LogWarning(e, "Video file not found: {filename}", filename);
            }
            else
            {
                logger.LogError(e, "Failed to check video file: {filename}", filename);
            }
            return false;
        }
    }

    public async Task<bool> DeleteVideoBlobAsync(string filename, CancellationToken cancellation = default)
    {
        try
        {
            await minioClient.RemoveObjectAsync(new RemoveObjectArgs()
                              .WithBucket(_options.BucketName_Private)
                              .WithObject($"videos/{filename}"), cancellation);
            return true;
        }
        catch (MinioException e)
        {
            if (e is ObjectNotFoundException)
            {
                logger.LogWarning(e, "Video file not found: {filename}", filename);
            }
            else
            {
                logger.LogError(e, "Failed to delete video file: {filename}", filename);
            }
            return false;
        }
    }

    public async Task UploadPublicFileAsync(string? contentType, string pathInStorage, string tempPath, CancellationToken cancellation = default)
    {
        try
        {
            var response = await minioClient.PutObjectAsync(new PutObjectArgs()
                                             .WithBucket(_options.BucketName_Public)
                                             .WithObject(pathInStorage)
                                             .WithFileName(tempPath)
                                             .WithContentType(contentType), cancellation);
        }
        catch (MinioException e)
        {
            logger.LogError(e, "Failed to upload public file: {filePath}", pathInStorage);
        }
    }
}
