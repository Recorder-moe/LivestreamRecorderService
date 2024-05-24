#if COSMOSDB
using LivestreamRecorder.DB.CosmosDB;
#elif COUCHDB
using LivestreamRecorder.DB.CouchDB;
#endif
using System.Net.Http.Json;
using HtmlAgilityPack;
using LivestreamRecorder.DB.Enums;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Json;
using LivestreamRecorderService.Models;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;
using Serilog.Context;

namespace LivestreamRecorderService.ScopedServices.PlatformService;

public class TwitcastingService(
    ILogger<TwitcastingService> logger,
    IHttpClientFactory httpClientFactory,
    // ReSharper disable once SuggestBaseTypeForParameterInConstructor
    UnitOfWork_Public unitOfWorkPublic,
    IVideoRepository videoRepository,
    // ReSharper disable once SuggestBaseTypeForParameterInConstructor
    ITwitcastingRecorderService twitcastingRecorderService,
    IStorageService storageService,
    IChannelRepository channelRepository,
    IOptions<DiscordOption> discordOptions,
    IServiceProvider serviceProvider) : PlatformService(channelRepository,
                                                        storageService,
                                                        httpClientFactory,
                                                        logger,
                                                        discordOptions,
                                                        serviceProvider), IPlatformService
{
    private const string StreamServerApi = "https://twitcasting.tv/streamserver.php";
#pragma warning disable CA1859 // 盡可能使用具象類型以提高效能
    private readonly IUnitOfWork _unitOfWorkPublic = unitOfWorkPublic;
#pragma warning restore CA1859 // 盡可能使用具象類型以提高效能

    public override string PlatformName => "Twitcasting";
    public override int Interval => 10;

    public override async Task UpdateVideosDataAsync(Channel channel, CancellationToken cancellation = default)
    {
        using IDisposable ____ = LogContext.PushProperty("Platform", PlatformName);
        using IDisposable __ = LogContext.PushProperty("channelId", channel.id);

        (bool isLive, string? videoId) = await GetTwitcastingLiveStatusAsync(channel, cancellation);
        using IDisposable ___ = LogContext.PushProperty("videoId", videoId);

        if (!isLive || string.IsNullOrEmpty(videoId))
        {
            logger.LogTrace("{channelId} is down.", channel.id);
            return;
        }

        Video? video = await videoRepository.GetVideoByIdAndChannelIdAsync(videoId, channel.id);

        if (null != video)
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
                    // All cases should be handled
                    logger.LogWarning("{videoId} is in {status}.", video.id, Enum.GetName(typeof(VideoStatus), video.Status));
                    return;
            }
        else
            video = new Video
            {
                id = videoId,
                Source = PlatformName,
                Status = VideoStatus.Missing,
                SourceStatus = VideoStatus.Unknown,
                IsLiveStream = true,
                Title = "",
                ChannelId = channel.id,
                Timestamps = new Timestamps
                {
                    PublishedAt = DateTime.UtcNow,
                    ActualStartTime = DateTime.UtcNow
                }
            };

        video.Thumbnail =
            await DownloadThumbnailAsync(
                $"https://twitcasting.tv/{NameHelper.ChangeId.ChannelId.PlatformType(channel.id, PlatformName)}/thumb/{NameHelper.ChangeId.VideoId.PlatformType(videoId, PlatformName)}",
                video.id,
                cancellation);

        if (await GetTwitcastingIsPublicAsync(video, cancellation))
        {
            (string title, string telop) = await GetTwitcastingStreamTitleAsync(video, cancellation);
            if (string.IsNullOrEmpty(video.Title)) video.Title = title;
            video.Description ??= telop;
            video.SourceStatus = VideoStatus.Exist;

            if (isLive && (video.Status < VideoStatus.Recording
                           || video.Status == VideoStatus.Missing))
            {
                await twitcastingRecorderService.CreateJobAsync(video: video,
                                                                useCookiesFile: false,
                                                                cancellation: cancellation);

                video.Status = VideoStatus.Recording;
                logger.LogInformation("{channelId} is now lived! Start recording.", channel.id);

                if (null != DiscordService) await DiscordService.SendStartRecordingMessageAsync(video, channel);
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

    public override async Task UpdateVideoDataAsync(Video video, CancellationToken cancellation = default)
    {
        await videoRepository.ReloadEntityFromDBAsync(video);
        Channel? channel = await ChannelRepository.GetChannelByIdAndSourceAsync(video.ChannelId, video.Source);
        video.Timestamps.ActualStartTime ??= video.Timestamps.PublishedAt;

        if (string.IsNullOrEmpty(video.Thumbnail))
            video.Thumbnail =
                await DownloadThumbnailAsync(
                    $"https://twitcasting.tv/{NameHelper.ChangeId.ChannelId.PlatformType(video.ChannelId, PlatformName)}/thumb/{NameHelper.ChangeId.VideoId.PlatformType(video.id, PlatformName)}",
                    video.id,
                    cancellation);

        try
        {
            if (await GetTwitcastingIsPublishAsync(video, cancellation))
            {
                if (await GetTwitcastingIsPublicAsync(video, cancellation))
                {
                    (string title, string telop) = await GetTwitcastingStreamTitleAsync(video, cancellation);
                    if (string.IsNullOrEmpty(video.Title)) video.Title = title;
                    video.Description = telop;
                    video.SourceStatus = VideoStatus.Exist;

                    if (video.Status <= VideoStatus.Pending) video.Status = VideoStatus.WaitingToDownload;
                }
                else
                {
                    // 有發佈，但是有瀏覧限制
                    // First detected
                    if (video.SourceStatus != VideoStatus.Reject)
                    {
                        video.SourceStatus = VideoStatus.Reject;
                        video.Note = "Video source is detected access required.";
                        if (null != DiscordService) await DiscordService.SendDeletedMessageAsync(video, channel);

                        logger.LogInformation("Video source is {status} because it is detected access required.",
                                              Enum.GetName(typeof(VideoStatus), video.SourceStatus));
                    }

                    video.SourceStatus = VideoStatus.Reject;
                }
            }
            else
            {
                if (video.SourceStatus != VideoStatus.Deleted)
                {
                    if (video.Status >= VideoStatus.Archived
                        && video.Status < VideoStatus.Expired)
                    {
                        video.SourceStatus = VideoStatus.Deleted;
                        if (null != DiscordService)
                            // First detected
                            await DiscordService.SendDeletedMessageAsync(video, channel);
                    }

                    video.SourceStatus = VideoStatus.Deleted;
                    video.Note = "Video is not published.";
                    logger.LogInformation("Twitcasting video {videoId} is not published.", video.id);
                }

                if (video.Status <= VideoStatus.Recording)
                    video.Status = VideoStatus.Missing;
            }
        }
        catch (HttpRequestException e)
        {
            logger.LogError(e,
                            "Failed to get twitcasting video {videoId} webpage. {channelId} Be careful if this happens repeatedly.",
                            video.id,
                            video.ChannelId);
        }

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
        HtmlDocument? htmlDoc = new HtmlWeb().Load($"https://twitcasting.tv/{NameHelper.ChangeId.ChannelId.PlatformType(channel.id, PlatformName)}");
        if (null == htmlDoc)
        {
            logger.LogWarning("Failed to get channel page for {channelId}", channel.id);
            return;
        }

        string channelId = channel.id;

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
            HtmlNode? avatarImgNode = htmlDoc.DocumentNode.SelectSingleNode("//a[@class='tw-user-nav-icon']/img");
            string? avatarUrl = avatarImgNode?.Attributes["src"]?.Value
                                             .Replace("_bigger", "");

            if (string.IsNullOrEmpty(avatarUrl)) return null;

            avatarBlobUrl = await DownloadImageAndUploadToBlobStorageAsync(avatarUrl, $"avatar/{channelId}", stoppingToken);

            return avatarBlobUrl;
        }

        async Task<string?> getBannerBlobUrl()
        {
            HtmlNode? bannerNode = htmlDoc.DocumentNode.SelectSingleNode("//div[@class='tw-user-banner-image']");
            string? bannerUrl = extractBackgroundImageUrl(bannerNode?.GetAttributeValue("style", "") ?? "");
            if (string.IsNullOrEmpty(bannerUrl)) return null;

            bannerBlobUrl = await DownloadImageAndUploadToBlobStorageAsync(bannerUrl, $"banner/{channelId}", stoppingToken);

            return bannerBlobUrl;
        }

        static string? extractBackgroundImageUrl(string style)
        {
            if (string.IsNullOrEmpty(style)) return null;

            const string searchString = "background-image: url(";
            int startIndex = style.IndexOf(searchString, StringComparison.Ordinal);
            if (startIndex == -1) return null;

            startIndex += searchString.Length;
            int endIndex = style.IndexOf(')', startIndex);
            if (endIndex == -1) return null;

            string url = style[startIndex..endIndex].Trim('\'', '\"');
            if (url.StartsWith("//")) url = "https:" + url;

            return url;
        }

        string? getChannelName()
            => htmlDoc.DocumentNode.SelectSingleNode("//span[@class='tw-user-nav-name']").InnerText;
    }

    private async Task<(bool Live, string? Id)> GetTwitcastingLiveStatusAsync(Channel channel, CancellationToken cancellation = default)
    {
        try
        {
            using HttpClient client = HttpClientFactory.CreateClient();
            HttpResponseMessage response =
                await client.GetAsync(
                    $@"{StreamServerApi}?target={NameHelper.ChangeId.ChannelId.PlatformType(channel.id, PlatformName)}&mode=client",
                    cancellation);

            response.EnsureSuccessStatusCode();
            TwitcastingStreamData? data =
                await response.Content.ReadFromJsonAsync(SourceGenerationContext.Default.TwitcastingStreamData, cancellationToken: cancellation);

            return data?.Movie.Id == null
                ? (false, null)
                : (data.Movie.Live ?? false, NameHelper.ChangeId.VideoId.DatabaseType(data.Movie.Id.Value.ToString(), PlatformName));
        }
        catch (HttpRequestException e)
        {
            logger.LogError(e,
                            "Get twitcasting live status failed with {StatusCode}. {channelId} Be careful if this happens repeatedly.",
                            e.StatusCode,
                            channel.id);

            return (false, null);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Get twitcasting live status failed. {channelId} Be careful if this happens repeatedly.", channel.id);
            return (false, null);
        }
    }

    private async Task<(string title, string telop)> GetTwitcastingStreamTitleAsync(Video video, CancellationToken cancellation = default)
    {
        using HttpClient client = HttpClientFactory.CreateClient();

        HttpResponseMessage response =
            await client.GetAsync(
                $@"https://twitcasting.tv/{NameHelper.ChangeId.ChannelId.PlatformType(video.ChannelId, PlatformName)}/movie/{NameHelper.ChangeId.VideoId.PlatformType(video.id, PlatformName)}",
                cancellation);

        if (!response.IsSuccessStatusCode)
            return ("(Unknown)", "");

        string responseBody = await response.Content.ReadAsStringAsync(cancellation);

        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(responseBody);

        HtmlNode? ogTitleNode = htmlDocument.DocumentNode.SelectSingleNode("//head/meta[@property='og:title']");
        HtmlNode? descriptionNode = htmlDocument.DocumentNode.SelectSingleNode("//head/meta[@name='description']");

        string title = ogTitleNode?.Attributes["content"]?.Value.Trim() ?? "(Unknown)";
        string description = descriptionNode?.Attributes["content"]?.Value.Trim() ?? "";

        return (title, description);
    }

    /// <summary>
    ///     檢查影片是否公開(沒有密碼鎖或是瀏覧限制)
    /// </summary>
    /// <param name="video"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    private async Task<bool> GetTwitcastingIsPublicAsync(Video video, CancellationToken cancellation = default)
    {
        try
        {
            // Web page will contain this string if the video is password locked
            const string keyword = "tw-empty-state-action";

            using HttpClient client = HttpClientFactory.CreateClient();
            HttpResponseMessage response =
                await client.GetAsync(
                    $"https://twitcasting.tv/{NameHelper.ChangeId.ChannelId.PlatformType(video.ChannelId, PlatformName)}/movie/{NameHelper.ChangeId.VideoId.PlatformType(video.id, PlatformName)}",
                    cancellation);

            response.EnsureSuccessStatusCode();

            string data = await response.Content.ReadAsStringAsync(cancellation);
            return !data.Contains(keyword);
        }
        catch (HttpRequestException e)
        {
            logger.LogError(e,
                            "Get twitcasting IsPublic failed with {StatusCode}. {channelId} Be careful if this happens repeatedly.",
                            e.StatusCode,
                            video.ChannelId);

            throw;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Get twitcasting IsPublic failed. {channelId} Be careful if this happens repeatedly.", video.ChannelId);
            throw;
        }
    }

    /// <summary>
    ///     檢查影片是否發佈
    /// </summary>
    /// <param name="video"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    private async Task<bool> GetTwitcastingIsPublishAsync(Video video, CancellationToken cancellation = default)
    {
        try
        {
            // Web page will contains this string if the video is not published
            var keyword = "tw-player-empty-message";

            using HttpClient client = HttpClientFactory.CreateClient();
            HttpResponseMessage response =
                await client.GetAsync(
                    $"https://twitcasting.tv/{NameHelper.ChangeId.ChannelId.PlatformType(video.ChannelId, PlatformName)}/movie/{NameHelper.ChangeId.VideoId.PlatformType(video.id, PlatformName)}",
                    cancellation);

            response.EnsureSuccessStatusCode();

            string data = await response.Content.ReadAsStringAsync(cancellation);
            return !data.Contains(keyword);
        }
        catch (HttpRequestException e)
        {
            logger.LogError(e,
                            "Get twitcasting IsPublish failed with {StatusCode}. {channelId} Be careful if this happens repeatedly.",
                            e.StatusCode,
                            video.ChannelId);

            throw;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Get twitcasting IsPublish failed. {channelId} Be careful if this happens repeatedly.", video.ChannelId);
            throw;
        }
    }
}
