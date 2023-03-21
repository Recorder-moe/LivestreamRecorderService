using Discord;
using Discord.Webhook;
using LivestreamRecorderService.DB.Enum;
using LivestreamRecorderService.DB.Models;
using LivestreamRecorderService.Models.OptionDiscords;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices;

public class DiscordService
{
    private readonly DiscordWebhookClient _discordWebhookClient;
    private readonly ILogger<DiscordService> _logger;
    private readonly DiscordOption _discordOption;
    private readonly AzureOption _azureOption;

    public DiscordService(
        DiscordWebhookClient discordWebhookClient,
        ILogger<DiscordService> logger,
        IOptions<DiscordOption> options,
        IOptions<AzureOption> azureOptions)
    {
        _discordWebhookClient = discordWebhookClient;
        _logger = logger;
        _discordWebhookClient.Log += this.DiscordWebhookClient_Log;
        _discordOption = options.Value;
        _azureOption = azureOptions.Value;
    }

    /// <summary>
    /// 把.NET Core logger對應到Discord內建的logger上面
    /// </summary>
    /// <param name="arg"></param>
    /// <returns></returns>
    private Task DiscordWebhookClient_Log(LogMessage arg)
        => Task.Run(() =>
           {
               switch (arg.Severity)
               {
                   case LogSeverity.Critical:
                       _logger.LogCritical("{message}", arg);
                       break;
                   case LogSeverity.Error:
                       _logger.LogError("{message}", arg);
                       break;
                   case LogSeverity.Warning:
                       _logger.LogWarning("{message}", arg);
                       break;
                   case LogSeverity.Info:
                       _logger.LogInformation("{message}", arg);
                       break;
                   case LogSeverity.Verbose:
                       _logger.LogTrace("{message}", arg);
                       break;
                   case LogSeverity.Debug:
                   default:
                       _logger.LogDebug("{message}", arg);
                       break;
               }
           });

    public Task SendStartRecordingMessage(Video video)
    {
        var embedBuilder = GetEmbedBuilder(video);
        embedBuilder.WithTitle("Start Recording");
        embedBuilder.WithColor(Color.Orange);

        var componentBuilder = GetComponentBuilder(video);

        return SendMessage(embedBuilder.Build(), componentBuilder.Build());
    }

    public Task SendArchivedMessage(Video video)
    {
        var embedBuilder = GetEmbedBuilder(video);
        embedBuilder.WithTitle("Video archived");
        embedBuilder.WithColor(Color.Green);

        var componentBuilder = GetComponentBuilder(video);

        return SendMessage(embedBuilder.Build(), componentBuilder.Build());
    }
    
    public Task SendDeletedMessage(Video video)
    {
        var embedBuilder = GetEmbedBuilder(video);
        embedBuilder.WithTitle("Source " + Enum.GetName(typeof(VideoStatus), video.SourceStatus??VideoStatus.Unknown));
        embedBuilder.WithColor(Color.DarkGrey);

        var componentBuilder = GetComponentBuilder(video);

        return SendMessage(embedBuilder.Build(), componentBuilder.Build());
    }

    public Task SendChannelSupportTokenAlertMessage(Channel channel)
    {
        var embedBuilder = GetEmbedBuilder(channel);
        embedBuilder.WithTitle("The support token is about to run out.");
        embedBuilder.WithColor(Color.Gold);

        var componentBuilder = GetComponentBuilder(channel);

        return SendMessage(embedBuilder.Build(), componentBuilder.Build());
    }

    public Task SendChannelSupportTokenZeroMessage(Channel channel)
    {
        var embedBuilder = GetEmbedBuilder(channel);
        embedBuilder.WithTitle("The support token has been exhausted.");
        embedBuilder.WithColor(Color.Red);

        var componentBuilder = GetComponentBuilder(channel);

        return SendMessage(embedBuilder.Build(), componentBuilder.Build());
    }

    private EmbedBuilder GetEmbedBuilder(Video video)
    {
        EmbedBuilder embedBuilder = new();
        embedBuilder.WithImageUrl($"https://{_azureOption.StorageAccountName}.blob.core.windows.net/{_azureOption.BlobContainerNamePublic}/thumbnails/{video.Thumbnail}");
        embedBuilder.WithDescription(video.Title);
        embedBuilder.AddField("Video ID", video.id, false);
        embedBuilder.AddField("Channel", video.Channel.ChannelName, true);
        embedBuilder.AddField("Channel ID", video.ChannelId, true);
        embedBuilder.AddField("Source", video.Source, true);
        embedBuilder.AddField("Is live stream", video.IsLiveStream, true);
        if (null != video.Timestamps.PublishedAt)
            embedBuilder.AddField("Published at", video.Timestamps.PublishedAt?.ToString("yyyy/MM/dd HH:mm:ss"), true);
        if (null != video.Timestamps.ScheduledStartTime)
            embedBuilder.AddField("Schduled start time", video.Timestamps.ScheduledStartTime?.ToString("yyyy/MM/dd HH:mm:ss"), true);
        if (null != video.Timestamps.ActualStartTime)
            embedBuilder.AddField("Actual start time", video.Timestamps.ActualStartTime?.ToString("yyyy/MM/dd HH:mm:ss"), true);

        return embedBuilder;
    }

    private EmbedBuilder GetEmbedBuilder(Channel channel)
    {
        EmbedBuilder embedBuilder = new();
        embedBuilder.WithImageUrl($"https://{_azureOption.StorageAccountName}.blob.core.windows.net/{_azureOption.BlobContainerNamePublic}/avatar/{channel.Avatar}");
        embedBuilder.WithDescription(channel.ChannelName);
        embedBuilder.AddField("Channel ID", channel.id, true);
        embedBuilder.AddField("Source", channel.Source, true);
        embedBuilder.AddField("Is monitoring", channel.Monitoring, true);
        embedBuilder.AddField("Support Token", channel.SupportToken, false);

        return embedBuilder;
    }

    private ComponentBuilder GetComponentBuilder(Video video)
    {
        ComponentBuilder componentBuilder = new();
        componentBuilder.WithButton(label: "Recorder.moe",
                                    url: $"https://{_discordOption.FrontEndHost}/channels/{video.ChannelId}/videos/{video.id}",
                                    emote: Emote.Parse(_discordOption.Emotes.RecorderMoe));
        componentBuilder.WithButton(label: video.Source,
                                    url: video.Source switch
                                    {
                                        "Youtube" => $"https://www.youtube.com/watch?v={video.id}",
                                        "Twitcasting" => $"https://twitcasting.tv/{video.ChannelId}/movie/{video.id}",
                                        "Twitch" => $"https://twitch.tv/{video.ChannelId}",
                                        _ => ""
                                    },
                                    emote: video.Source switch
                                    {
                                        "Youtube" => Emote.Parse(_discordOption.Emotes.Youtube),
                                        "Twitcasting" => Emote.Parse(_discordOption.Emotes.Twitcasting),
                                        "Twitch" => Emote.Parse(_discordOption.Emotes.Twitch),
                                        _ => ""
                                    });
        return componentBuilder;
    }

    private ComponentBuilder GetComponentBuilder(Channel channel)
    {
        ComponentBuilder componentBuilder = new();
        componentBuilder.WithButton(label: "Recorder.moe",
                                    style: ButtonStyle.Link,
                                    url: $"https://{_discordOption.FrontEndHost}/channels/{channel.id}",
                                    emote: Emote.Parse(_discordOption.Emotes.RecorderMoe));
        componentBuilder.WithButton(label: channel.Source,
                                    url: channel.Source switch
                                    {
                                        "Youtube" => $"https://www.youtube.com/channel/{channel.id}",
                                        "Twitcasting" => $"https://twitcasting.tv/{channel.id}",
                                        "Twitch" => $"https://twitch.tv/{channel.id}",
                                        _ => ""
                                    },
                                    emote: channel.Source switch
                                    {
                                        "Youtube" => Emote.Parse(_discordOption.Emotes.Youtube),
                                        "Twitcasting" => Emote.Parse(_discordOption.Emotes.Twitcasting),
                                        "Twitch" => Emote.Parse(_discordOption.Emotes.Twitch),
                                        _ => ""
                                    });
        return componentBuilder;
    }

    async Task SendMessage(Embed embed, MessageComponent component)
    {
        ulong messageId = await _discordWebhookClient.SendMessageAsync(embeds: new Embed[] { embed },
                                                                       username: "Recorder.moe Notifier",
                                                                       avatarUrl: $"https://{_discordOption.FrontEndHost}/assets/img/logos/logo-color-big.png",
                                                                       components: component);
        _logger.LogDebug("Message sent to discord: {title}, {messageId}", embed.Title, messageId);
    }
}
