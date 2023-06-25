using LivestreamRecorder.DB.Core;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorderService.Models.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using System.Configuration;

namespace LivestreamRecorderService.DependencyInjection
{
    public static partial class Extensions
    {
        public static IServiceCollection AddCosmosDB(this IServiceCollection services, IConfiguration configuration)
        {
            try
            {
                var azureOptions = services.BuildServiceProvider().GetRequiredService<IOptions<AzureOption>>().Value;

                if (null == azureOptions.CosmosDB
                    || string.IsNullOrEmpty(configuration.GetConnectionString("Public"))
                    || string.IsNullOrEmpty(configuration.GetConnectionString("Private")))
                    throw new ConfigurationErrorsException();

                // Add CosmosDB
                services.AddDbContext<PublicContext>((options) =>
                {
                    options
                        //.EnableSensitiveDataLogging()
                        .UseCosmos(connectionString: configuration.GetConnectionString("Public")!,
                                   databaseName: azureOptions.CosmosDB.Public.DatabaseName,
                                   cosmosOptionsAction: option => option.GatewayModeMaxConnectionLimit(380));
                });
                services.AddDbContext<PrivateContext>((options) =>
                {
                    options
                        //.EnableSensitiveDataLogging()
                        .UseCosmos(connectionString: configuration.GetConnectionString("Private")!,
                                   databaseName: azureOptions.CosmosDB.Private.DatabaseName,
                                   cosmosOptionsAction: option => option.GatewayModeMaxConnectionLimit(380));
                });

                services.AddScoped<UnitOfWork_Public>();
                services.AddScoped<UnitOfWork_Private>();
                services.AddScoped<IVideoRepository, VideoRepository>();
                services.AddScoped<IChannelRepository, ChannelRepository>();
                services.AddScoped<IUserRepository, UserRepository>();
                return services;
            }
            catch (ConfigurationErrorsException)
            {
                Log.Fatal("Missing CosmosDB Settings. Please set CosmosDB and ConnectionStrings:Public ConnectionStrings:Private in appsettings.json.");
                throw new ConfigurationErrorsException("Missing CosmosDB Settings. Please set CosmosDB and ConnectionStrings:Public ConnectionStrings:Private in appsettings.json.");
            }
        }
    }
}
