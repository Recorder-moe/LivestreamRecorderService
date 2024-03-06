using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.Workers;
using Serilog;
using System.Configuration;

namespace LivestreamRecorderService.DependencyInjection;

public static partial class Extensions
{
    public static IServiceCollection AddHeartbeatWorker(this IServiceCollection services, IConfiguration configuration)
    {
        try
        {
            IConfigurationSection config = configuration.GetSection(HeartbeatOption.ConfigurationSectionName);
            var heartbeatOptions = config.Get<HeartbeatOption>();
            if (null == heartbeatOptions) throw new ConfigurationErrorsException();

            services.AddOptions<HeartbeatOption>()
                    .Bind(config)
                    .ValidateDataAnnotations()
                    .ValidateOnStart();

            if (!heartbeatOptions.Enabled)
            {
                return services;
            }

            services.AddHostedService<HeartbeatWorker>();
            return services;
        }
        catch (ConfigurationErrorsException)
        {
            Log.Fatal("Missing Twitch ClientId or ClientSecret. Please set Twitch:ClientId and Twitch:ClientSecret in appsettings.json.");
            throw new ConfigurationErrorsException("Missing Twitch ClientId or ClientSecret. Please set Twitch:ClientId and Twitch:ClientSecret in appsettings.json.");
        }
    }
}
