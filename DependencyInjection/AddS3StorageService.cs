using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.SingletonServices;
using Microsoft.Extensions.Options;
using Minio;
using Serilog;
using System.Configuration;

namespace LivestreamRecorderService.DependencyInjection
{
    public static partial class Extensions
    {
        public static IServiceCollection AddS3StorageService(this IServiceCollection services)
        {
            try
            {
                var s3Options = services.BuildServiceProvider().GetRequiredService<IOptions<S3Option>>().Value;
                if (string.IsNullOrEmpty(s3Options.Endpoint)
                    || string.IsNullOrEmpty(s3Options.AccessKey)
                    || string.IsNullOrEmpty(s3Options.SecretKey)
                    || string.IsNullOrEmpty(s3Options.BucketName_Public)
                    || string.IsNullOrEmpty(s3Options.BucketName_Private))
                    throw new ConfigurationErrorsException();

                MinioClient minio = new MinioClient()
                                            .WithEndpoint(s3Options.Endpoint)
                                            .WithCredentials(s3Options.AccessKey, s3Options.SecretKey)
                                            .WithSSL(s3Options.Secure)
                                            .Build();

                services.AddSingleton<IMinioClient>(minio);
                services.AddSingleton<IStorageService, S3Service>();

                return services;
            }
            catch (ConfigurationErrorsException)
            {
                Log.Fatal("Missing S3. Please set S3 in appsettings.json.");
                throw new ConfigurationErrorsException("Missing S3. Please set S3 in appsettings.json.");
            }
        }
    }
}
