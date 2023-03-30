using Discord;
using Discord.Webhook;
using LivestreamRecorderService.DB.Enum;
using LivestreamRecorderService.DB.Models;
using LivestreamRecorderService.Models.OptionDiscords;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace LivestreamRecorderService.SingletonServices;

public partial class DiscordService
{
    private readonly ILogger<DiscordService> _logger;
    private readonly DiscordOption _discordOption;
    private readonly AzureOption _azureOption;
    private readonly DiscordWebhookClient _discordWebhookClient;
    private readonly DiscordWebhookClient _discordWebhookClientWarning;

    public DiscordService(
        ILogger<DiscordService> logger,
        IOptions<DiscordOption> options,
        IOptions<AzureOption> azureOptions)
    {
        _logger = logger;
        _discordOption = options.Value;
        _azureOption = azureOptions.Value;
        _discordWebhookClient = new DiscordWebhookClient(_discordOption.Webhook);
        _discordWebhookClient.Log += DiscordWebhookClient_Log;
        _discordWebhookClientWarning = new DiscordWebhookClient(_discordOption.WebhookWarning);
        _discordWebhookClientWarning.Log += DiscordWebhookClient_Log;
    }

    #region Log mapping
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
    #endregion

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

    public Task SendSkippedMessage(Video video)
    {
        var embedBuilder = GetEmbedBuilder(video);
        embedBuilder.WithTitle("Video skipped");
        embedBuilder.WithColor(Color.LightGrey);

        var componentBuilder = GetComponentBuilder(video);

        return SendMessage(embedBuilder.Build(), componentBuilder.Build(), video.Note ?? "");
    }

    public Task SendDeletedMessage(Video video)
    {
        var embedBuilder = GetEmbedBuilder(video);
        embedBuilder.WithTitle("Source " + Enum.GetName(typeof(VideoStatus), video.SourceStatus ?? VideoStatus.Unknown));
        embedBuilder.WithColor(Color.DarkGrey);

        var componentBuilder = GetComponentBuilder(video);

        return SendMessageWarning(embedBuilder.Build(), componentBuilder.Build(), $"{_discordOption.Mention.Deleted} {video.Note}");
    }

    public Task SendChannelSupportTokenAlertMessage(Channel channel)
    {
        var embedBuilder = GetEmbedBuilder(channel);
        embedBuilder.WithTitle($"{channel.ChannelName} has {channel.SupportToken} ST.");
        embedBuilder.WithColor(Color.Gold);

        var componentBuilder = GetComponentBuilder(channel);

        return SendMessageWarning(embedBuilder.Build(), componentBuilder.Build(), $"{_discordOption.Mention.Channel} The support token is about to run out.");
    }

    public Task SendChannelSupportTokenZeroMessage(Channel channel)
    {
        var embedBuilder = GetEmbedBuilder(channel);
        embedBuilder.WithTitle($"{channel.ChannelName} has {channel.SupportToken} ST.");
        embedBuilder.WithColor(Color.Red);

        var componentBuilder = GetComponentBuilder(channel);

        return SendMessageWarning(embedBuilder.Build(), componentBuilder.Build(), $"{_discordOption.Mention.Channel} The support token has been exhausted.");
    }

    private EmbedBuilder GetEmbedBuilder(Video video)
    {
        EmbedBuilder embedBuilder = new();
        if(null != video.Thumbnail)
            embedBuilder.WithImageUrl($"https://{_azureOption.StorageAccountName}.blob.core.windows.net/{_azureOption.BlobContainerNamePublic}/thumbnails/{video.Thumbnail}");
        else
            embedBuilder.WithImageUrl($"https://{_azureOption.StorageAccountName}.blob.core.windows.net/{_azureOption.BlobContainerNamePublic}/banner/{video.Channel.Banner}");

        embedBuilder.WithDescription(video.Title);
        embedBuilder.WithUrl($"https://{_discordOption.FrontEndHost}/channels/{video.ChannelId}/videos/{video.id}");
        embedBuilder.AddField("Video ID", video.id, false);
        embedBuilder.AddField("Channel", video.Channel.ChannelName, false);
        embedBuilder.AddField("Channel ID", video.ChannelId, true);
        embedBuilder.AddField("Source", video.Source, true);
        embedBuilder.AddField("Is live stream", video.IsLiveStream?.ToString() ?? "Unknown", true);
        if (null != video.Timestamps.PublishedAt)
            embedBuilder.AddField("Published at", video.Timestamps.PublishedAt?.ToString("yyyy/MM/dd HH:mm:ss"), true);
        //if (null != video.Timestamps.ScheduledStartTime)
        //    embedBuilder.AddField("Schduled start time", video.Timestamps.ScheduledStartTime?.ToString("yyyy/MM/dd HH:mm:ss"), true);
        if (null != video.Timestamps.ActualStartTime)
            embedBuilder.AddField("Start time", video.Timestamps.ActualStartTime?.ToString("yyyy/MM/dd HH:mm:ss"), true);

        return embedBuilder;
    }

    private EmbedBuilder GetEmbedBuilder(Channel channel)
    {
        EmbedBuilder embedBuilder = new();
        embedBuilder.WithImageUrl($"https://{_azureOption.StorageAccountName}.blob.core.windows.net/{_azureOption.BlobContainerNamePublic}/avatar/{channel.Avatar}");
        embedBuilder.WithDescription(channel.ChannelName);
        embedBuilder.WithUrl($"https://{_discordOption.FrontEndHost}/channels/{channel.id}");
        embedBuilder.AddField("Channel ID", channel.id, true);
        embedBuilder.AddField("Source", channel.Source, true);
        embedBuilder.AddField("Is monitoring", channel.Monitoring, true);
        embedBuilder.AddField("Support Token", channel.SupportToken, false);

        return embedBuilder;
    }

    #region Buttons are not showing.
    // The button can only be displayed in the webhook generated by the bot, not in the webhook generated by the User.
    // https://github.com/discordjs/discord.js/issues/7097#issuecomment-993903477
    private ComponentBuilder GetComponentBuilder(Video video)
    {
        ComponentBuilder componentBuilder = new();
        componentBuilder.WithButton(label: "Recorder.moe",
                                    style: ButtonStyle.Link,
                                    url: $"https://{_discordOption.FrontEndHost}/channels/{video.ChannelId}/videos/{video.id}",
                                    emote: Emote.Parse(_discordOption.Emotes.RecorderMoe));
        componentBuilder.WithButton(label: video.Source,
                                    style: ButtonStyle.Link,
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
                                    style: ButtonStyle.Link,
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
    #endregion

    async Task SendMessage(Embed embed, MessageComponent component, string text = "")
    {
        ulong messageId = await _discordWebhookClient.SendMessageAsync(
            text: text,
            embeds: new Embed[] { embed },
            username: "Recorder.moe Notifier",
            avatarUrl: $"https://{_discordOption.FrontEndHost}/assets/img/logos/logo-color-big.png",
            components: component);
        _logger.LogDebug("Message sent to discord: {title}, {messageId}", embed.Title, messageId);
    }

    async Task SendMessageWarning(Embed embed, MessageComponent component, string? text)
    {
        AllowedMentions allowedMentions = new()
        {
            RoleIds = new List<string?>()
            {
                _discordOption.Mention.Deleted,
                _discordOption.Mention.Channel
            }.Where(p => !string.IsNullOrEmpty(p))
             .Select(input => ulong.Parse(RegexNumbers().Match(input!).Value))
             .ToList()
        };
        ulong messageId = await _discordWebhookClientWarning.SendMessageAsync(
            text: text,
            embeds: new Embed[] { embed },
            username: "Recorder.moe Notifier",
            avatarUrl: $"https://{_discordOption.FrontEndHost}/assets/img/logos/logo-color-big.png",
            allowedMentions: allowedMentions,
            components: component);
        _logger.LogDebug("Message warning sent to discord: {title}, {messageId}", embed.Title, messageId);
    }

    [GeneratedRegex("\\d+")]
    private static partial Regex RegexNumbers();
}
