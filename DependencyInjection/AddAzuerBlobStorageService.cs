using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.SingletonServices;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Options;
using Serilog;
using System.Configuration;

namespace LivestreamRecorderService.DependencyInjection
{
    public static partial class Extensions
    {
        public static IServiceCollection AddAzuerBlobStorageService(this IServiceCollection services)
        {
            try
            {
                var azureOptions = services.BuildServiceProvider().GetRequiredService<IOptions<AzureOption>>().Value;
                if (null == azureOptions.AzuerBlobStorage
                    || string.IsNullOrEmpty(azureOptions.AzuerBlobStorage.StorageAccountName)
                    || string.IsNullOrEmpty(azureOptions.AzuerBlobStorage.StorageAccountKey)
                    || string.IsNullOrEmpty(azureOptions.AzuerBlobStorage.BlobContainerNamePublic)
                    || string.IsNullOrEmpty(azureOptions.AzuerBlobStorage.BlobContainerName))
                    throw new ConfigurationErrorsException();

                services.AddAzureClients(clientsBuilder
                    => clientsBuilder.AddBlobServiceClient(azureOptions.AzuerBlobStorage.ConnectionString));

                services.AddSingleton<IStorageService, ABSService>();
                return services;
            }
            catch (ConfigurationErrorsException)
            {
                Log.Fatal("Missing AzuerBlobStorage. Please set Azure:AzuerBlobStorage in appsettings.json.");
                throw new ConfigurationErrorsException("Missing AzuerBlobStorage. Please set Azure:AzuerBlobStorage in appsettings.json.");
            }
        }
    }
}
