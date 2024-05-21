using System.Configuration;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.SingletonServices;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Options;
using Serilog;

namespace LivestreamRecorderService.DependencyInjection;

public static partial class Extensions
{
    public static IServiceCollection AddAzureBlobStorageService(this IServiceCollection services)
    {
        try
        {
            AzureOption azureOptions = services.BuildServiceProvider().GetRequiredService<IOptions<AzureOption>>().Value;
            if (null == azureOptions.BlobStorage
                || string.IsNullOrEmpty(azureOptions.BlobStorage.StorageAccountName)
                || string.IsNullOrEmpty(azureOptions.BlobStorage.StorageAccountKey)
                || string.IsNullOrEmpty(azureOptions.BlobStorage.BlobContainerName_Public)
                || string.IsNullOrEmpty(azureOptions.BlobStorage.BlobContainerName_Private))
                throw new ConfigurationErrorsException();

            services.AddAzureClients(clientsBuilder
                                         => clientsBuilder.AddBlobServiceClient(azureOptions.BlobStorage.ConnectionString));

            services.AddSingleton<IStorageService, AbsService>();

            return services;
        }
        catch (ConfigurationErrorsException)
        {
            Log.Fatal("Missing AzureBlobStorage. Please set Azure:AzureBlobStorage in appsettings.json.");
            throw new ConfigurationErrorsException("Missing AzureBlobStorage. Please set Azure:AzureBlobStorage in appsettings.json.");
        }
    }
}
