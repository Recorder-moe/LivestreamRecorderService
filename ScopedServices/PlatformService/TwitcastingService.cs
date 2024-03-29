﻿#if COSMOSDB
using LivestreamRecorder.DB.CosmosDB;
#elif COUCHDB
using LivestreamRecorder.DB.CouchDB;
#endif
using HtmlAgilityPack;
using LivestreamRecorder.DB.Enums;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Interfaces.Job.Downloader;
using LivestreamRecorderService.Models.OptionDiscords;
using Microsoft.Extensions.Options;
using Serilog.Context;
using System.Net.Http.Json;
using LivestreamRecorderService.Json;
using LivestreamRecorderService.Helper;

namespace LivestreamRecorderService.ScopedServices.PlatformService;

public class TwitcastingService(
    ILogger<TwitcastingService> logger,
    IHttpClientFactory httpClientFactory,
    UnitOfWork_Public unitOfWork_Public,
    IVideoRepository videoRepository,
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
#pragma warning disable CA1859 // 盡可能使用具象類型以提高效能
    private readonly IUnitOfWork _unitOfWork_Public = unitOfWork_Public;
#pragma warning restore CA1859 // 盡可能使用具象類型以提高效能

    public override string PlatformName => "Twitcasting";
    public override int Interval => 10;

    private const string _streamServerApi = "https://twitcasting.tv/streamserver.php";

    public override async Task UpdateVideosDataAsync(Channel channel, CancellationToken cancellation = default)
    {
        using var ____ = LogContext.PushProperty("Platform", PlatformName);
        using var __ = LogContext.PushProperty("channelId", channel.id);

        var (isLive, videoId) = await GetTwitcastingLiveStatusAsync(channel, cancellation);
        using var ___ = LogContext.PushProperty("videoId", videoId);

        if (!isLive || string.IsNullOrEmpty(videoId))
        {
            logger.LogTrace("{channelId} is down.", channel.id);
            return;
        }

        if (null != videoId)
        {
            Video? video = await videoRepository.GetVideoByIdAndChannelIdAsync(videoId, channel.id);

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
                    case VideoStatus.Uploading:
                        logger.LogTrace("{videoId} is uploading. We cannot change the video state during uploading. The status will be corrected after it is archived.", video.id);
                        return;
                    case VideoStatus.Archived:
                    case VideoStatus.PermanentArchived:
                        logger.LogWarning("{videoId} has already been archived. It is possible that an internet disconnect occurred during the process. Changed its state back to Recording.", video.id);
                        video.Status = VideoStatus.WaitingToRecord;
                        break;
                    default:
                        logger.LogWarning("{videoId} is in {status}.", video.id, Enum.GetName(typeof(VideoStatus), video.Status));
                        return;
                }
            }
            else
            {
                video = new Video()
                {
                    id = videoId,
                    Source = PlatformName,
                    Status = VideoStatus.Missing,
                    SourceStatus = VideoStatus.Unknown,
                    IsLiveStream = true,
                    Title = null!,
                    ChannelId = channel.id,
                    Timestamps = new Timestamps()
                    {
                        PublishedAt = DateTime.UtcNow,
                        ActualStartTime = DateTime.UtcNow
                    },
                };
            }

            video.Thumbnail = await DownloadThumbnailAsync($"https://twitcasting.tv/{NameHelper.ChangeId.ChannelId.PlatformType(channel.id, PlatformName)}/thumb/{NameHelper.ChangeId.VideoId.PlatformType(videoId, PlatformName)}", video.id, cancellation);

            if (await GetTwitcastingIsPublicAsync(video, cancellation))
            {
                var (title, telop) = await GetTwitcastingStreamTitleAsync(video, cancellation);
                video.Title ??= title ?? "";
                video.Description ??= telop ?? "";
                video.SourceStatus = VideoStatus.Exist;

                if (isLive && (video.Status < VideoStatus.Recording
                               || video.Status == VideoStatus.Missing))
                {
                    await twitcastingRecorderService.InitJobAsync(url: videoId,
                                                                   video: video,
                                                                   useCookiesFile: false,
                                                                   cancellation: cancellation);

                    video.Status = VideoStatus.Recording;
                    logger.LogInformation("{channelId} is now lived! Start recording.", channel.id);

                    if (null != discordService)
                    {
                        await discordService.SendStartRecordingMessageAsync(video, channel);
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
            _unitOfWork_Public.Commit();
        }
    }

    private async Task<(bool Live, string? Id)> GetTwitcastingLiveStatusAsync(Channel channel, CancellationToken cancellation = default)
    {
        try
        {
            using var client = httpClientFactory.CreateClient();
            var response = await client.GetAsync($@"{_streamServerApi}?target={NameHelper.ChangeId.ChannelId.PlatformType(channel.id, PlatformName)}&mode=client", cancellation);
            response.EnsureSuccessStatusCode();
            var data = await response.Content.ReadFromJsonAsync(SourceGenerationContext.Default.TwitcastingStreamData, cancellationToken: cancellation);

            return null == data || null == data.Movie.Id
                    ? (false, null)
                    : (data.Movie.Live ?? false, NameHelper.ChangeId.VideoId.DatabaseType(data.Movie.Id.Value.ToString(), PlatformName));
        }
        catch (HttpRequestException e)
        {
            logger.LogError(e, "Get twitcasting live status failed with {StatusCode}. {channelId} Be careful if this happens repeatedly.", e.StatusCode, channel.id);
            return (false, null);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Get twitcasting live status failed. {channelId} Be careful if this happens repeatedly.", channel.id);
            return (false, null);
        }
    }

    private async Task<(string? title, string? telop)> GetTwitcastingStreamTitleAsync(Video video, CancellationToken cancellation = default)
    {
        using var client = httpClientFactory.CreateClient();

        var response = await client.GetAsync($@"https://twitcasting.tv/{NameHelper.ChangeId.ChannelId.PlatformType(video.ChannelId, PlatformName)}/movie/{NameHelper.ChangeId.VideoId.PlatformType(video.id, PlatformName)}", cancellation);

        if (!response.IsSuccessStatusCode)
            return ("(Unknown)", "");

        string responseBody = await response.Content.ReadAsStringAsync(cancellation);

        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(responseBody);

        var ogTitleNode = htmlDocument.DocumentNode.SelectSingleNode("//head/meta[@property='og:title']");
        var descriptionNode = htmlDocument.DocumentNode.SelectSingleNode("//head/meta[@name='description']");

        string title = ogTitleNode?.Attributes["content"]?.Value.Trim() ?? "(Unknown)";
        string description = descriptionNode?.Attributes["content"]?.Value.Trim() ?? "";

        return (title, description);
    }

    /// <summary>
    /// 檢查影片是否公開(沒有密碼鎖或是瀏覧限制)
    /// </summary>
    /// <param name="videoId"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    private async Task<bool> GetTwitcastingIsPublicAsync(Video video, CancellationToken cancellation = default)
    {
        try
        {
            // Web page will contains this string if the video is password locked
            var keyword = "tw-empty-state-action";

            using var client = httpClientFactory.CreateClient();
            var response = await client.GetAsync($"https://twitcasting.tv/{NameHelper.ChangeId.ChannelId.PlatformType(video.ChannelId, PlatformName)}/movie/{NameHelper.ChangeId.VideoId.PlatformType(video.id, PlatformName)}", cancellation);
            response.EnsureSuccessStatusCode();

            var data = await response.Content.ReadAsStringAsync(cancellation);
            return !data.Contains(keyword);
        }
        catch (HttpRequestException e)
        {
            logger.LogError(e, "Get twitcasting IsPublic failed with {StatusCode}. {channelId} Be careful if this happens repeatedly.", e.StatusCode, video.ChannelId);
            throw;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Get twitcasting IsPublic failed. {channelId} Be careful if this happens repeatedly.", video.ChannelId);
            throw;
        }
    }

    /// <summary>
    /// 檢查影片是否發佈
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

            using var client = httpClientFactory.CreateClient();
            var response = await client.GetAsync($"https://twitcasting.tv/{NameHelper.ChangeId.ChannelId.PlatformType(video.ChannelId, PlatformName)}/movie/{NameHelper.ChangeId.VideoId.PlatformType(video.id, PlatformName)}", cancellation);
            response.EnsureSuccessStatusCode();

            var data = await response.Content.ReadAsStringAsync(cancellation);
            return !data.Contains(keyword);
        }
        catch (HttpRequestException e)
        {
            logger.LogError(e, "Get twitcasting IsPublish failed with {StatusCode}. {channelId} Be careful if this happens repeatedly.", e.StatusCode, video.ChannelId);
            throw;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Get twitcasting IsPublish failed. {channelId} Be careful if this happens repeatedly.", video.ChannelId);
            throw;
        }
    }

    public override async Task UpdateVideoDataAsync(Video video, CancellationToken cancellation = default)
    {
        await videoRepository.ReloadEntityFromDBAsync(video);
        var channel = await channelRepository.GetChannelByIdAndSourceAsync(video.ChannelId, video.Source);
        if (null == video.Timestamps.ActualStartTime)
        {
            video.Timestamps.ActualStartTime = video.Timestamps.PublishedAt;
        }

        if (string.IsNullOrEmpty(video.Thumbnail))
        {
            video.Thumbnail = await DownloadThumbnailAsync($"https://twitcasting.tv/{NameHelper.ChangeId.ChannelId.PlatformType(video.ChannelId, PlatformName)}/thumb/{NameHelper.ChangeId.VideoId.PlatformType(video.id, PlatformName)}", video.id, cancellation);
        }

        try
        {
            if (await GetTwitcastingIsPublishAsync(video, cancellation))
            {
                if (await GetTwitcastingIsPublicAsync(video, cancellation))
                {
                    var (title, telop) = await GetTwitcastingStreamTitleAsync(video, cancellation);
                    video.Title = title ?? video.Title;
                    video.Description = telop ?? video.Description;
                    video.SourceStatus = VideoStatus.Exist;

                    if (video.Status <= VideoStatus.Pending)
                    {
                        video.Status = VideoStatus.WaitingToDownload;
                    }
                }
                else
                {
                    // 有發佈，但是有瀏覧限制
                    // First detected
                    if (video.SourceStatus != VideoStatus.Reject)
                    {
                        video.SourceStatus = VideoStatus.Reject;
                        video.Note = "Video source is detected access required.";
                        if (null != discordService)
                        {
                            await discordService.SendDeletedMessageAsync(video, channel);
                        }
                        logger.LogInformation("Video source is {status} because it is detected access required.", Enum.GetName(typeof(VideoStatus), video.SourceStatus));
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
                        if (null != discordService)
                        {
                            // First detected
                            await discordService.SendDeletedMessageAsync(video, channel);
                        }
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
            logger.LogError(e, "Failed to get twitcasting video {videoId} webpage. {channelId} Be careful if this happens repeatedly.", video.id, video.ChannelId);
        }

        if (!string.IsNullOrEmpty(video.Filename))
        {
            if (!await storageService.IsVideoFileExistsAsync(video.Filename, cancellation))
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
                logger.LogInformation("Correct video status to {status} because archived is exists.", Enum.GetName(typeof(VideoStatus), video.Status));
            }
        }

        await videoRepository.AddOrUpdateAsync(video);
        _unitOfWork_Public.Commit();
    }

    public override Task UpdateChannelDataAsync(Channel channel, CancellationToken stoppingToken)
        => throw new InvalidOperationException();
}
