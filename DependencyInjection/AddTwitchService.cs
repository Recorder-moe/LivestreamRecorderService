using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.ScopedServices.PlatformService;
using Serilog;
using System.Configuration;
using TwitchLib.Api;
using TwitchLib.Api.Core;
using TwitchLib.Api.Interfaces;

namespace LivestreamRecorderService.DependencyInjection;

public static partial class Extensions
{
    public static IServiceCollection AddTwitchService(this IServiceCollection services, IConfiguration configuration)
    {
        try
        {
            IConfigurationSection config = configuration.GetSection(TwitchOption.ConfigurationSectionName);
            var twitchOptions = config.Get<TwitchOption>();
            if (null == twitchOptions) throw new ConfigurationErrorsException();

            services.AddOptions<TwitchOption>()
                    .Bind(config)
                    .ValidateDataAnnotations()
                    .ValidateOnStart();

            if (!twitchOptions.Enabled)
            {
                return services;
            }

            if (string.IsNullOrEmpty(twitchOptions.ClientId)
                || string.IsNullOrEmpty(twitchOptions.ClientSecret))
                throw new ConfigurationErrorsException();

            services.AddSingleton<ITwitchAPI, TwitchAPI>(s =>
            {
                var api = new TwitchAPI(
                    loggerFactory: s.GetRequiredService<ILoggerFactory>(),
                    settings: new ApiSettings()
                    {
                        ClientId = twitchOptions.ClientId,
                        Secret = twitchOptions.ClientSecret
                    });
                return api;
            });
            services.AddScoped<TwitchService>();
            return services;
        }
        catch (ConfigurationErrorsException)
        {
            Log.Fatal("Missing Twitch ClientId or ClientSecret. Please set Twitch:ClientId and Twitch:ClientSecret in appsettings.json.");
            throw new ConfigurationErrorsException("Missing Twitch ClientId or ClientSecret. Please set Twitch:ClientId and Twitch:ClientSecret in appsettings.json.");
        }
    }
}
