using LivestreamRecorderService.Models.OptionDiscords;
using LivestreamRecorderService.SingletonServices;
using Serilog;
using System.Configuration;

namespace LivestreamRecorderService.DependencyInjection;

public static partial class Extensions
{
    public static IServiceCollection AddDiscordService(this IServiceCollection services, IConfiguration configuration)
    {
        try
        {
            IConfigurationSection config = configuration.GetSection(DiscordOption.ConfigurationSectionName);
            var discordOptions = config.Get<DiscordOption>();
            if (null == discordOptions) throw new ConfigurationErrorsException();

            services.AddOptions<DiscordOption>()
                    .Bind(config)
                    .ValidateDataAnnotations()
                    .ValidateOnStart();

            if (!discordOptions.Enabled)
            {
                return services;
            }

            if (string.IsNullOrEmpty(discordOptions.Webhook)
                || string.IsNullOrEmpty(discordOptions.WebhookWarning)
                || string.IsNullOrEmpty(discordOptions.WebhookAdmin)
                || string.IsNullOrEmpty(discordOptions.FrontEndHost)
                || null == discordOptions.Mention
                || null == discordOptions.Emotes
            ) throw new ConfigurationErrorsException();

            services.AddSingleton<DiscordService>();
            return services;
        }
        catch (ConfigurationErrorsException)
        {
            Log.Fatal("Missing Discord Settings. Please set Discord:Enabled Discord:Webhook, Discord:WebhookWarning, Discord:WebhookAdmin, Discord:FrontEndHost, Discord:Mention and Discord:Emotes in appsettings.json.");
            throw new ConfigurationErrorsException("Missing Discord Settings. Please set Discord:Enabled Discord:Webhook, Discord:WebhookWarning, Discord:WebhookAdmin, Discord:FrontEndHost, Discord:Mention and Discord:Emotes in appsettings.json.");
        }
    }
}
