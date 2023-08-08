#if COSMOSDB
using LivestreamRecorder.DB.CosmosDB;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Models.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
#endif
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
#if COSMOSDB
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
                services.AddScoped<IVideoRepository>((s) => new VideoRepository((IUnitOfWork)s.GetRequiredService(typeof(UnitOfWork_Public))));
                services.AddScoped<IChannelRepository>((s) => new ChannelRepository((IUnitOfWork)s.GetRequiredService(typeof(UnitOfWork_Public))));
                services.AddScoped<IUserRepository>((s) => new UserRepository((IUnitOfWork)s.GetRequiredService(typeof(UnitOfWork_Private))));
#endif
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
