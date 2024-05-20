#if COSMOSDB
using LivestreamRecorder.DB.CosmosDB;
#elif COUCHDB
using LivestreamRecorder.DB.CouchDB;
#endif
using LivestreamRecorder.DB.Enums;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Interfaces.Job.Downloader;
using LivestreamRecorderService.Json;
using LivestreamRecorderService.Models;
using Microsoft.Extensions.Options;
using Serilog.Context;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using LivestreamRecorderService.Models.Options;

namespace LivestreamRecorderService.ScopedServices.PlatformService;

public class Fc2Service(
    ILogger<Fc2Service> logger,
    // ReSharper disable once SuggestBaseTypeForParameterInConstructor
    UnitOfWork_Public unitOfWorkPublic,
    // ReSharper disable once SuggestBaseTypeForParameterInConstructor
    IFc2LiveDLService fC2LiveDLService,
    IStorageService storageService,
    IHttpClientFactory httpClientFactory,
    IOptions<DiscordOption> discordOptions,
    IChannelRepository channelRepository,
    IVideoRepository videoRepository,
    IServiceProvider serviceProvider) : PlatformService(channelRepository,
    storageService,
    httpClientFactory,
    logger,
    discordOptions,
    serviceProvider), IPlatformService
{
#pragma warning disable CA1859 // 盡可能使用具象類型以提高效能
    private readonly IUnitOfWork _unitOfWorkPublic = unitOfWorkPublic;
#pragma warning restore CA1859 // 盡可能使用具象類型以提高效能
    private readonly IChannelRepository _channelRepository = channelRepository;
    private readonly IStorageService _storageService = storageService;
    private readonly IHttpClientFactory _httpFactory = httpClientFactory;

    public override string PlatformName => "FC2";

    public override int Interval => 10;

    private const string MemberApi = "https://live.fc2.com/api/memberApi.php";

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = $"{nameof(SourceGenerationContext)} is set.")]
    public override async Task UpdateVideosDataAsync(Channel channel, CancellationToken cancellation = default)
    {
        using var ____ = LogContext.PushProperty("Platform", PlatformName);
        using var __ = LogContext.PushProperty("channelId", channel.id);

        logger.LogTrace("Start to get FC2 stream: {channelId}", channel.id);

        var (isLive, videoId) = await GetFc2LiveStatusAsync(channel, cancellation);
        using var ___ = LogContext.PushProperty("videoId", videoId);

        if (!isLive || string.IsNullOrEmpty(videoId))
        {
            logger.LogTrace("{channelId} is down.", channel.id);
            return;
        }

        if (!string.IsNullOrEmpty(videoId))
        {
            var video = await videoRepository.GetVideoByIdAndChannelIdAsync(videoId, channel.id);
            if (null != video)
            {
                switch (video.Status)
                {
                    case VideoStatus.WaitingToRecord:
                    case VideoStatus.Recording:
                        logger.LogTrace("{channelId} is already recording.", channel.id);
                        return;
                    case VideoStatus.Reject:
                    case VideoStatus.Skipped:
                        logger.LogTrace("{videoId} is rejected for recording.", video.id);
                        return;
                    case VideoStatus.Archived:
                    case VideoStatus.PermanentArchived:
                        logger.LogWarning(
                            "{videoId} has already been archived. It is possible that an internet disconnect occurred during the process. Changed its state back to Recording.",
                            video.id);

                        video.Status = VideoStatus.WaitingToRecord;
                        break;

                    case VideoStatus.Unknown:
                    case VideoStatus.Scheduled:
                    case VideoStatus.Pending:
                    case VideoStatus.WaitingToDownload:
                    case VideoStatus.Downloading:
                    case VideoStatus.Expired:
                    case VideoStatus.Missing:
                    case VideoStatus.Error:
                    case VideoStatus.Exist:
                    case VideoStatus.Edited:
                    case VideoStatus.Deleted:
                    default:
                        // All cases should be handled
                        break;
                }
            }
            else
            {
                video = new Video()
                {
                    id = videoId,
                    Source = PlatformName,
                    Status = VideoStatus.Missing,
                    SourceStatus = VideoStatus.Deleted,
                    IsLiveStream = true,
                    Title = null!,
                    ChannelId = channel.id,
                    Timestamps = new Timestamps()
                    {
                        PublishedAt = DateTime.UtcNow,
                        ActualStartTime = DateTime.UtcNow
                    },
                };

                logger.LogTrace("New video found: {videoId}", video.id);
            }

            await videoRepository.AddOrUpdateAsync(video);
            _unitOfWorkPublic.Commit();

            var info = await GetFc2InfoDataAsync(channel.id, cancellation);
            if (null == info) return;

            video.Thumbnail = await DownloadThumbnailAsync(info.Data.ChannelData.Image, video.id, cancellation);
            video.Title = info.Data.ChannelData.Title;
            video.Description = info.Data.ChannelData.Info;
            video.Timestamps.ActualStartTime =
                0 == info.Data.ChannelData.Start
                || null == info.Data.ChannelData.Start
                    ? DateTime.UtcNow
                    : DateTimeOffset.FromUnixTimeMilliseconds((long)info.Data.ChannelData.Start).UtcDateTime;

            if (info.Data.ChannelData.IsLimited == 0
                && info.Data.ChannelData.IsPremium == 0)
            {
                if (video.Status < VideoStatus.Recording
                    || video.Status == VideoStatus.Missing)
                {
                    await fC2LiveDLService.InitJobAsync(
                        url: $"https://live.fc2.com/{NameHelper.ChangeId.ChannelId.PlatformType(channel.id, PlatformName)}/",
                        video: video,
                        useCookiesFile: channel.UseCookiesFile == true,
                        cancellation: cancellation);

                    video.Status = VideoStatus.Recording;
                    logger.LogInformation("{channelId} is now lived! Start recording.", channel.id);
                    logger.LogDebug("fc2Info: {info}",
                        JsonSerializer.Serialize(
                            info,
#pragma warning disable CA1869
                            options: new JsonSerializerOptions
#pragma warning restore CA1869
                            {
                                TypeInfoResolver = SourceGenerationContext.Default
                            }));

                    if (null != DiscordService)
                    {
                        await DiscordService.SendStartRecordingMessageAsync(video, channel);
                    }
                }
            }
            else
            {
                video.Status = VideoStatus.Skipped;
                video.SourceStatus = VideoStatus.Reject;
                video.Note = "Video skipped because it is detected not public.";
                logger.LogWarning("This video is not public! Skip {videoId}", videoId);
            }

            await videoRepository.AddOrUpdateAsync(video);
            _unitOfWorkPublic.Commit();
        }
    }

    private async Task<(bool Live, string? Id)> GetFc2LiveStatusAsync(Channel channel, CancellationToken cancellation = default)
    {
        var info = await GetFc2InfoDataAsync(channel.id, cancellation);

        var start = info?.Data.ChannelData.Start?.ToString();

        return null == info || string.IsNullOrEmpty(start) || start == "0"
            ? (false, null)
            : (info.Data.ChannelData.IsPublish == 1, NameHelper.ChangeId.VideoId.DatabaseType(start, PlatformName));
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = $"{nameof(SourceGenerationContext)} is set.")]
    private async Task<FC2MemberData?> GetFc2InfoDataAsync(string channelId, CancellationToken cancellation = default)
    {
        try
        {
            using var client = _httpFactory.CreateClient();
            var response = await client.PostAsync(
                requestUri: $@"{MemberApi}",
                content: new FormUrlEncodedContent(
                    new Dictionary<string, string>()
                    {
                        { "channel", "1" },
                        { "profile", "1" },
                        { "user", "0" },
                        { "streamid", NameHelper.ChangeId.ChannelId.PlatformType(channelId, PlatformName) }
                    }),
                cancellationToken: cancellation);

            response.EnsureSuccessStatusCode();
            var jsonString = await response.Content.ReadAsStringAsync(cancellation);
            var info = JsonSerializer.Deserialize<FC2MemberData>(
                jsonString,
                options: new()
                {
                    TypeInfoResolver = SourceGenerationContext.Default
                });

            return info;
        }
        catch (HttpRequestException e)
        {
            logger.LogError(e, "Get fc2 info failed with {StatusCode}. {channelId} Be careful if this happens repeatedly.", e.StatusCode, channelId);
            return null;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Get fc2 info failed. {channelId} Be careful if this happens repeatedly.", channelId);
            return null;
        }
    }

    public override async Task UpdateVideoDataAsync(Video video, CancellationToken cancellation = default)
    {
        await videoRepository.ReloadEntityFromDBAsync(video);
        if (null == video.Timestamps.ActualStartTime)
        {
            video.Timestamps.ActualStartTime = video.Timestamps.PublishedAt;
        }

        if (video.Status <= VideoStatus.Pending)
        {
            video.Status = VideoStatus.WaitingToDownload;
            if (NameHelper.ChangeId.VideoId.PlatformType(video.id, PlatformName).StartsWith("20"))
            {
                var videoData =
                    await GetVideoInfoByYtdlpAsync(
                        $"https://video.fc2.com/content/{NameHelper.ChangeId.VideoId.PlatformType(video.id, PlatformName)}",
                        cancellation);

                if (null != videoData)
                    video.Thumbnail = await DownloadThumbnailAsync(videoData.Thumbnail, video.id, cancellation);
            }
        }

        if (!string.IsNullOrEmpty(video.Filename))
        {
            if (!await _storageService.IsVideoFileExistsAsync(video.Filename, cancellation))
            {
                if (video.Status >= VideoStatus.Archived && video.Status < VideoStatus.Expired)
                {
                    video.Status = VideoStatus.Missing;
                    video.Note = "Video missing because archived not found.";
                    logger.LogInformation("Can not found archived, change video status to {status}", Enum.GetName(typeof(VideoStatus), video.Status));
                }
            }
            else if (video.Status < VideoStatus.Archived || video.Status >= VideoStatus.Expired)
            {
                video.Status = VideoStatus.Archived;
                video.Note = null;
                logger.LogInformation("Correct video status to {status} because archived is exists.",
                    Enum.GetName(typeof(VideoStatus), video.Status));
            }
        }

        await videoRepository.AddOrUpdateAsync(video);
        _unitOfWorkPublic.Commit();
    }

    public override async Task UpdateChannelDataAsync(Channel channel, CancellationToken stoppingToken)
    {
        var avatarBlobUrl = channel.Avatar;
        var info = await GetFc2InfoDataAsync(channel.id, stoppingToken);
        if (null == info)
        {
            logger.LogWarning("Failed to get channel info for {channelId}", channel.id);
            return;
        }

        var avatarUrl = info.Data.ProfileData.Image;
        if (!string.IsNullOrEmpty(avatarUrl))
        {
            avatarBlobUrl = await DownloadImageAndUploadToBlobStorageAsync(avatarUrl, $"avatar/{channel.id}", stoppingToken);
        }

        channel = await _channelRepository.ReloadEntityFromDBAsync(channel) ?? channel;
        channel.ChannelName = info.Data.ProfileData.Name;
        channel.Avatar = avatarBlobUrl?.Replace("avatar/", "");
        await _channelRepository.AddOrUpdateAsync(channel);
        _unitOfWorkPublic.Commit();
    }
}
