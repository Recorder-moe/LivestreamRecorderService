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
        public static IServiceCollection AddAzureFileShareService(this IServiceCollection services)
        {
            try
            {
                var azureOptions = services.BuildServiceProvider().GetRequiredService<IOptions<AzureOption>>().Value;
                if (null == azureOptions.AzureFileShare
                    || string.IsNullOrEmpty(azureOptions.AzureFileShare.StorageAccountName)
                    || string.IsNullOrEmpty(azureOptions.AzureFileShare.StorageAccountKey)
                    || string.IsNullOrEmpty(azureOptions.AzureFileShare.ShareName))
                    throw new ConfigurationErrorsException();

                services.AddAzureClients(clientsBuilder
                    => clientsBuilder.AddFileServiceClient(azureOptions.AzureFileShare.ConnectionString));
                services.AddSingleton<IAFSService, AFSService>();
                return services;
            }
            catch (ConfigurationErrorsException)
            {
                Log.Fatal("Missing AzureFileShare. Please set Azure:AzureFileShare in appsettings.json.");
                throw new ConfigurationErrorsException("Missing AzureFileShare. Please set Azure:AzureFileShare in appsettings.json.");
            }
        }
    }
}
