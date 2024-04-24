#if COSMOSDB
using LivestreamRecorder.DB.CosmosDB;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Models.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Configuration;
#endif
using Serilog;

namespace LivestreamRecorderService.DependencyInjection;

public static partial class Extensions
{
    public static IServiceCollection AddCosmosDb(this IServiceCollection services, IConfiguration configuration)
    {
#if !COSMOSDB
        Log.Fatal("This is a CouchDB build. Please use the CosmosDB build for Azure CosmosDB support.");
        throw new InvalidOperationException("This is a CouchDB build. Please use the CosmosDB build for Azure CosmosDB support.");
#else
        try
        {
            var azureOptions = services.BuildServiceProvider().GetRequiredService<IOptions<AzureOption>>().Value;

            if (null == azureOptions.CosmosDB
                || null == azureOptions.CosmosDB.Public.ConnectionStrings
                || null == azureOptions.CosmosDB.Private.ConnectionStrings)
                throw new ConfigurationErrorsException();

            // Add CosmosDB
            services.AddDbContext<PublicContext>((options) =>
            {
                options
#if !RELEASE
                    .EnableSensitiveDataLogging()
#endif
                    .UseCosmos(connectionString: azureOptions.CosmosDB.Public.ConnectionStrings,
                        databaseName: azureOptions.CosmosDB.Public.DatabaseName,
                        cosmosOptionsAction: option => option.GatewayModeMaxConnectionLimit(380));
            });

            services.AddDbContext<PrivateContext>((options) =>
            {
                options
#if !RELEASE
                    .EnableSensitiveDataLogging()
#endif
                    .UseCosmos(connectionString: azureOptions.CosmosDB.Private.ConnectionStrings,
                        databaseName: azureOptions.CosmosDB.Private.DatabaseName,
                        cosmosOptionsAction: option => option.GatewayModeMaxConnectionLimit(380));
            });

            services.AddScoped<UnitOfWork_Public>();
            services.AddScoped<UnitOfWork_Private>();
            services.AddScoped<IVideoRepository>((s) => new VideoRepository((IUnitOfWork)s.GetRequiredService(typeof(UnitOfWork_Public))));
            services.AddScoped<IChannelRepository>((s) => new ChannelRepository((IUnitOfWork)s.GetRequiredService(typeof(UnitOfWork_Public))));
            services.AddScoped<IUserRepository>((s) => new UserRepository((IUnitOfWork)s.GetRequiredService(typeof(UnitOfWork_Private))));
            return services;
        }
        catch (ConfigurationErrorsException)
        {
            Log.Fatal("Missing CosmosDB Settings. Please setup CosmosDB in appsettings.json.");
            throw new ConfigurationErrorsException("Missing CosmosDB Settings. Please setup CosmosDB in appsettings.json.");
        }
#endif
    }
}
