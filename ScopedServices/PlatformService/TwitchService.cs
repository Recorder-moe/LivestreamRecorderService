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
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;
using Serilog.Context;
using TwitchLib.Api.Helix.Models.Streams.GetStreams;
using TwitchLib.Api.Helix.Models.Users.GetUsers;
using TwitchLib.Api.Interfaces;
using Stream = TwitchLib.Api.Helix.Models.Streams.GetStreams.Stream;
using User = TwitchLib.Api.Helix.Models.Users.GetUsers.User;

namespace LivestreamRecorderService.ScopedServices.PlatformService;

public class TwitchService(
    ILogger<TwitchService> logger,
    // ReSharper disable once SuggestBaseTypeForParameterInConstructor
    UnitOfWork_Public unitOfWorkPublic,
    IVideoRepository videoRepository,
    IChannelRepository channelRepository,
    // ReSharper disable once SuggestBaseTypeForParameterInConstructor
    IStreamlinkService streamlinkService,
    ITwitchAPI twitchApi,
    IStorageService storageService,
    IHttpClientFactory httpClientFactory,
    IOptions<DiscordOption> discordOptions,
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

    public override string PlatformName => "Twitch";

    public override int Interval => 60;

    public override async Task UpdateVideosDataAsync(Channel channel, CancellationToken cancellation = default)
    {
        using IDisposable ____ = LogContext.PushProperty("Platform", PlatformName);
        using IDisposable __ = LogContext.PushProperty("channelId", channel.id);

        logger.LogTrace("Start to get Twitch stream: {channelId}", channel.id);
        GetStreamsResponse? streams = await twitchApi.Helix.Streams.GetStreamsAsync(
            userLogins: [NameHelper.ChangeId.ChannelId.PlatformType(channel.id, PlatformName)]);

        if (null != streams
            && streams.Streams.Length > 0
            && streams.Streams.First() is Stream stream)
        {
            string videoId = NameHelper.ChangeId.VideoId.DatabaseType(stream.Id.TrimStart('v'), PlatformName);
            using IDisposable ___ = LogContext.PushProperty("videoId", videoId);

            Video? video = await videoRepository.GetVideoByIdAndChannelIdAsync(videoId, channel.id);

            if (null != video)
                switch (video.Status)
                {
                    case VideoStatus.WaitingToRecord:
                    case VideoStatus.Recording:
                        if (video.Title == stream.Title
                            && video.Description == stream.GameName
                            && null != video.Thumbnail)
                        {
                            logger.LogTrace("{channelId} is already recording.", channel.id);
                            return;
                        }

                        break;
                    case VideoStatus.Reject:
                    case VideoStatus.Skipped:
                        logger.LogTrace("{videoId} is rejected for recording.", video.id);
                        return;
                    case VideoStatus.Missing:
                        logger.LogWarning(
                            "{videoId} has been marked missing. It is possible that a server malfunction occurred during the previous recording. Changed its state back to Recording.",
                            video.id);

                        video.Status = VideoStatus.WaitingToRecord;
                        break;
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
                    //case VideoStatus.Uploading:
                    case VideoStatus.Expired:
                    case VideoStatus.Error:
                    case VideoStatus.Exist:
                    case VideoStatus.Edited:
                    case VideoStatus.Deleted:
                    default:
                        logger.LogWarning("{videoId} is in {status}, skip.", video.id, Enum.GetName(typeof(VideoStatus), video.Status));
                        return;
                }
            else
                video = new Video
                {
                    id = videoId,
                    Source = PlatformName,
                    Status = VideoStatus.WaitingToRecord,
                    SourceStatus = VideoStatus.Unknown,
                    IsLiveStream = true,
                    Title = stream.Title,
                    Description = stream.GameName,
                    Timestamps = new Timestamps
                    {
                        PublishedAt = stream.StartedAt,
                        ActualStartTime = stream.StartedAt
                    },

                    ChannelId = channel.id
                };

            video.Title = stream.Title;
            video.Description = stream.GameName;
            video.Timestamps.ActualStartTime = stream.StartedAt;
            video.Timestamps.PublishedAt = stream.StartedAt;
            video.Thumbnail = await DownloadThumbnailAsync(stream.ThumbnailUrl.Replace("-{width}x{height}", ""), video.id, cancellation);

            if (video.Status < VideoStatus.Recording
                || video.Status == VideoStatus.Missing)
            {
                await streamlinkService.CreateJobAsync(video: video,
                                                       useCookiesFile: false,
                                                       cancellation: cancellation);

                video.Status = VideoStatus.Recording;
                logger.LogInformation("{channelId} is now lived! Start recording.", channel.id);
                if (null != DiscordService
                    && channel.Hide != true)
                    await DiscordService.SendStartRecordingMessageAsync(video, channel);
            }

            await videoRepository.AddOrUpdateAsync(video);
            _unitOfWorkPublic.Commit();
        }
        else
        {
            logger.LogTrace("{channelId} is down.", channel.id);
        }
    }

    public override async Task UpdateVideoDataAsync(Video video, CancellationToken cancellation = default)
    {
        await videoRepository.ReloadEntityFromDBAsync(video);
        if (null == video.Timestamps.ActualStartTime) video.Timestamps.ActualStartTime = video.Timestamps.PublishedAt;

        if (video.Status <= VideoStatus.Pending) video.Status = VideoStatus.WaitingToDownload;

        if (!string.IsNullOrEmpty(video.Filename))
        {
            if (!await StorageService.IsVideoFileExistsAsync(video.Filename, cancellation))
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
        string channelId = channel.id;
        GetUsersResponse? usersResponse =
            await twitchApi.Helix.Users.GetUsersAsync(logins: [NameHelper.ChangeId.ChannelId.PlatformType(channelId, PlatformName)]);

        if (null == usersResponse || usersResponse.Users.Length == 0)
        {
            logger.LogWarning("Failed to get channel info for {channelId}", channelId);
            return;
        }

        User? user = usersResponse.Users.First();

        string? avatarBlobUrl = await getAvatarBlobUrl() ?? channel.Avatar;
        string? bannerBlobUrl = await getBannerBlobUrl() ?? channel.Banner;
        string channelName = getChannelName() ?? channel.ChannelName;

        channel = await ChannelRepository.ReloadEntityFromDBAsync(channel) ?? channel;
        channel.ChannelName = channelName;
        channel.Avatar = avatarBlobUrl?.Replace("avatar/", "");
        channel.Banner = bannerBlobUrl?.Replace("banner/", "");
        await ChannelRepository.AddOrUpdateAsync(channel);
        _unitOfWorkPublic.Commit();
        return;

        async Task<string?> getAvatarBlobUrl()
        {
            string avatarUrl = user.ProfileImageUrl.Replace("70x70", "300x300");
            if (string.IsNullOrEmpty(avatarUrl)) return null;

            avatarBlobUrl = await DownloadImageAndUploadToBlobStorageAsync(avatarUrl, $"avatar/{channelId}", stoppingToken);

            return avatarBlobUrl;
        }

        async Task<string?> getBannerBlobUrl()
        {
            string? bannerUrl = user.OfflineImageUrl;
            if (string.IsNullOrEmpty(bannerUrl)) return null;

            bannerBlobUrl = await DownloadImageAndUploadToBlobStorageAsync(bannerUrl, $"banner/{channelId}", stoppingToken);

            return bannerBlobUrl;
        }

        string? getChannelName()
            => user.DisplayName;
    }
}
