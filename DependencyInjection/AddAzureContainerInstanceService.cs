using Azure.Identity;
using Azure.ResourceManager;
using LivestreamRecorderService.Interfaces.Job;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.SingletonServices.ACI;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Options;
using Serilog;
using System.Configuration;

namespace LivestreamRecorderService.DependencyInjection
{
    public static partial class Extensions
    {
        public static IServiceCollection AddAzureContainerInstanceService(this IServiceCollection services)
        {
            try
            {
                var azureOptions = services.BuildServiceProvider().GetRequiredService<IOptions<AzureOption>>().Value;
                if (null == azureOptions.ContainerInstance
                    || string.IsNullOrEmpty(azureOptions.ContainerInstance.ClientSecret.ClientID)
                    || string.IsNullOrEmpty(azureOptions.ContainerInstance.ClientSecret.ClientSecret))
                    throw new ConfigurationErrorsException();

                services.AddAzureClients(clientsBuilder
                    => clientsBuilder.UseCredential((options)
                        => new ClientSecretCredential(tenantId: azureOptions.ContainerInstance.ClientSecret.TenantID,
                                                      clientId: azureOptions.ContainerInstance.ClientSecret.ClientID,
                                                      clientSecret: azureOptions.ContainerInstance.ClientSecret.ClientSecret))
                                     .AddClient<ArmClient, ArmClientOptions>((options, token) => new ArmClient(token)));

                services.AddSingleton<IJobService, ACIService>();

                services.AddSingleton<IYtarchiveService, YtarchiveService>();
                services.AddSingleton<IYtdlpService, YtdlpService>();
                services.AddSingleton<IStreamlinkService, StreamlinkService>();
                services.AddSingleton<ITwitcastingRecorderService, TwitcastingRecorderService>();
                services.AddSingleton<IFC2LiveDLService, FC2LiveDLService>();

                return services;
            }
            catch (ConfigurationErrorsException)
            {
                Log.Fatal("Missing AzureContainerInstance. Please set Azure:AzureContainerInstance in appsettings.json.");
                throw new ConfigurationErrorsException("Missing AzureContainerInstance. Please set Azure:AzureContainerInstance in appsettings.json.");
            }
        }
    }
}
