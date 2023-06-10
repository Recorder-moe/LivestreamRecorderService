using LivestreamRecorder.DB.Core;
using LivestreamRecorder.DB.Enum;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorderService.Models.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;

namespace LivestreamRecorderService.DependencyInjection
{
    public static partial class Extensions
    {
        public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
        {
            var serviceOptions = services.BuildServiceProvider().GetRequiredService<IOptions<ServiceOption>>().Value;

            if (serviceOptions.DatabaseService == ServiceName.AzureCosmosDB)
            {
                IConfigurationSection config = configuration.GetSection(CosmosDbOptions.ConfigurationSectionName);
                services.AddOptions<CosmosDbOptions>()
                    .Bind(config)
                    .ValidateDataAnnotations()
                    .ValidateOnStart();

                var cosmosDbOptions = config.Get<CosmosDbOptions>()!;

                // Add CosmosDb
                services.AddDbContext<PublicContext>((options) =>
                {
                    options
                        //.EnableSensitiveDataLogging()
                        .UseCosmos(connectionString: configuration.GetConnectionString("Public")!,
                                   databaseName: cosmosDbOptions.Public.DatabaseName,
                                   cosmosOptionsAction: option => option.GatewayModeMaxConnectionLimit(380));
                });
                services.AddDbContext<PrivateContext>((options) =>
                {
                    options
                        //.EnableSensitiveDataLogging()
                        .UseCosmos(connectionString: configuration.GetConnectionString("Private")!,
                                   databaseName: cosmosDbOptions.Private.DatabaseName,
                                   cosmosOptionsAction: option => option.GatewayModeMaxConnectionLimit(380));
                });

                services.AddScoped<UnitOfWork_Public>();
                services.AddScoped<UnitOfWork_Private>();
                services.AddScoped<IVideoRepository, VideoRepository>();
                services.AddScoped<IChannelRepository, ChannelRepository>();
                services.AddScoped<IUserRepository, UserRepository>();
            }
            else
            {
                Log.Fatal("Currently only Azure CosmosDB is supported.");
                throw new NotImplementedException("Currently only Azure CosmosDB is supported.");
            }
            return services;
        }
    }
}
