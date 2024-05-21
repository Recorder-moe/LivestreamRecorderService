using System.Configuration;
using Azure.Identity;
using Azure.ResourceManager;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.SingletonServices;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Options;
using Serilog;

namespace LivestreamRecorderService.DependencyInjection;

public static partial class Extensions
{
    public static IServiceCollection AddAzureContainerInstanceService(this IServiceCollection services)
    {
        try
        {
            AzureOption azureOptions = services.BuildServiceProvider().GetRequiredService<IOptions<AzureOption>>().Value;
            if (null == azureOptions.ContainerInstance
                || string.IsNullOrEmpty(azureOptions.ContainerInstance.ClientSecret.ClientID)
                || string.IsNullOrEmpty(azureOptions.ContainerInstance.ClientSecret.ClientSecret))
                throw new ConfigurationErrorsException();

            services.AddAzureClients(clientsBuilder
                                         => clientsBuilder.UseCredential(_
                                                                             => new ClientSecretCredential(
                                                                                 tenantId: azureOptions.ContainerInstance.ClientSecret.TenantID,
                                                                                 clientId: azureOptions.ContainerInstance.ClientSecret.ClientID,
                                                                                 clientSecret: azureOptions.ContainerInstance.ClientSecret
                                                                                     .ClientSecret))
                                                          .AddClient<ArmClient, ArmClientOptions>((_, token) => new ArmClient(token)));

            services.AddSingleton<IJobService, AciService>();

            return services;
        }
        catch (ConfigurationErrorsException)
        {
            Log.Fatal("Missing AzureContainerInstance. Please set Azure:AzureContainerInstance in appsettings.json.");
            throw new ConfigurationErrorsException("Missing AzureContainerInstance. Please set Azure:AzureContainerInstance in appsettings.json.");
        }
    }
}
