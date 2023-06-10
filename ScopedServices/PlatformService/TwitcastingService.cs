using LivestreamRecorder.DB.Core;
using LivestreamRecorder.DB.Enum;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Interfaces.Job;
using LivestreamRecorderService.Models;
using LivestreamRecorderService.Models.OptionDiscords;
using Microsoft.Extensions.Options;
using Serilog.Context;
using System.Net.Http.Json;

namespace LivestreamRecorderService.ScopedServices.PlatformService;

public class TwitcastingService : PlatformService, IPlatformService
{
    private readonly ILogger<TwitcastingService> _logger;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IUnitOfWork _unitOfWork_Public;
    private readonly IVideoRepository _videoRepository;
    private readonly ITwitcastingRecorderService _twitcastingRecorderService;
    private readonly IStorageService _storageService;

    public override string PlatformName => "Twitcasting";
    public override int Interval => 10;

    private const string _streamServerApi = "https://twitcasting.tv/streamserver.php";
    private const string _frontendApi = "https://frontendapi.twitcasting.tv";
    private const string _happytokenApi = "https://twitcasting.tv/happytoken.php";

    public TwitcastingService(
        ILogger<TwitcastingService> logger,
        IHttpClientFactory httpClientFactory,
        UnitOfWork_Public unitOfWork_Public,
        IVideoRepository videoRepository,
        ITwitcastingRecorderService twitcastingRecorderService,
        IStorageService storageService,
        IChannelRepository channelRepository,
        IOptions<DiscordOption> discordOptions,
        IServiceProvider serviceProvider) : base(channelRepository,
                                                 storageService,
                                                 httpClientFactory,
                                                 logger,
                                                 discordOptions,
                                                 serviceProvider)
    {
        _logger = logger;
        _httpFactory = httpClientFactory;
        _unitOfWork_Public = unitOfWork_Public;
        _videoRepository = videoRepository;
        _twitcastingRecorderService = twitcastingRecorderService;
        _storageService = storageService;
    }

    public override async Task UpdateVideosDataAsync(Channel channel, CancellationToken cancellation = default)
    {
        using var ____ = LogContext.PushProperty("Platform", PlatformName);
        using var __ = LogContext.PushProperty("channelId", channel.id);

        var (isLive, videoId) = await GetTwitcastingLiveStatusAsync(channel, cancellation);
        using var ___ = LogContext.PushProperty("videoId", videoId);

        if (null != videoId)
        {
            Video video;

            if (_videoRepository.Exists(videoId))
            {
                if (!isLive)
                {
                    _logger.LogTrace("{channelId} is down.", channel.id);
                    return;
                }

                video = _videoRepository.GetById(videoId);
                switch (video.Status)
                {
                    case VideoStatus.WaitingToRecord:
                    case VideoStatus.Recording:
                        _logger.LogTrace("{channelId} is already recording.", channel.id);
                        return;
                    case VideoStatus.Reject:
                    case VideoStatus.Skipped:
                        _logger.LogTrace("{videoId} is rejected for recording.", video.id);
                        return;
                    case VideoStatus.Archived:
                    case VideoStatus.PermanentArchived:
                        _logger.LogWarning("{videoId} is already archived.", video.id);
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
                    Channel = channel,
                    Timestamps = new Timestamps()
                    {
                        PublishedAt = DateTime.UtcNow,
                        ActualStartTime = DateTime.UtcNow
                    },
                };
            }

            video.Thumbnail = await DownloadThumbnailAsync($"https://twitcasting.tv/{channel.id}/thumb/{videoId}", video.id, cancellation);

            if (await GetTwitcastingIsPublicAsync(videoId, cancellation))
            {
                var (title, telop) = await GetTwitcastingStreamTitleAsync(videoId, cancellation);
                video.Title ??= title ?? "";
                video.Description ??= telop ?? "";
                video.SourceStatus = VideoStatus.Exist;

                if (isLive && (video.Status < VideoStatus.Recording
                               || video.Status == VideoStatus.Missing))
                {
                    _ = _twitcastingRecorderService.InitJobAsync(url: videoId,
                                                                          channelId: video.ChannelId,
                                                                          useCookiesFile: false,
                                                                          cancellation: cancellation);

                    video.Status = VideoStatus.Recording;
                    _logger.LogInformation("{channelId} is now lived! Start recording.", channel.id);

                    if (null != discordService)
                    {
                        await discordService.SendStartRecordingMessage(video);
                    }
                }
            }
            else
            {
                video.Status = VideoStatus.Skipped;
                video.SourceStatus = VideoStatus.Reject;
                video.Note = "Video skipped because it is detected not public.";
                _logger.LogWarning("This video is not public! Skip {videoId}", videoId);
            }

            _videoRepository.AddOrUpdate(video);
            _unitOfWork_Public.Commit();
        }
    }

    // Example
    // https://github.com/kkent030315/twitcasting-py/blob/main/src/twitcasting.py
    private async Task<(bool Live, string? Id)> GetTwitcastingLiveStatusAsync(Channel channel, CancellationToken cancellation = default)
    {
        try
        {
            using var client = _httpFactory.CreateClient();
            var response = await client.GetAsync($@"{_streamServerApi}?target={channel.id}&mode=client", cancellation);
            response.EnsureSuccessStatusCode();
            var data = await response.Content.ReadFromJsonAsync<TwitcastingStreamData>(cancellationToken: cancellation);

            return null == data
                    ? (false, null)
                    : (data.Movie.Live ?? false, data.Movie.Id.ToString());
        }
        catch (Exception)
        {
            _logger.LogError("Get twitcasting live status failed. {channelId} Be careful if this happens repeatedly.", channel.id);
            return (false, null);
        }
    }

    private async Task<(string? title, string? telop)> GetTwitcastingStreamTitleAsync(string videoId, CancellationToken cancellation = default)
    {
        using var client = _httpFactory.CreateClient();

        var token = await GetTwitcastingTokenAsync(videoId, cancellation);
        if (null == token)
        {
            _logger.LogWarning("Failed to get video title because token in null! {videoId}", videoId);
            return default;
        }

        var response = await client.GetAsync($@"{_frontendApi}/movies/{videoId}/status/viewer?token={token}", cancellation);
        var data = await response.Content.ReadFromJsonAsync<TwitcastingViewerData>(cancellationToken: cancellation);

        return !string.IsNullOrEmpty(data?.Movie.Title)
                ? (data?.Movie.Title, data?.Movie.Telop)
                : (data?.Movie.Telop, "");
    }

    private async Task<string?> GetTwitcastingTokenAsync(string videoId, CancellationToken cancellation = default)
    {
        using var client = _httpFactory.CreateClient();
        int epochTimeStamp = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
        using var content = new MultipartFormDataContent("------WebKitFormBoundary")
        {
            { new StringContent(videoId), "movie_id" }
        };
        try
        {
            var response = await client.PostAsync($@"{_happytokenApi}?__n={epochTimeStamp}", content, cancellation);
            response.EnsureSuccessStatusCode();
            var data = await response.Content.ReadFromJsonAsync<TwitcastingTokenData>(cancellationToken: cancellation);

            return null != data
                    && !string.IsNullOrEmpty(data.Token)
                        ? data.Token
                        : throw new Exception();
        }
        catch (Exception)
        {
            _logger.LogWarning("Failed to get Twitcasting token!");
        }
        return null;
    }

    /// <summary>
    /// 檢查影片是否公開(沒有密碼鎖或是瀏覧限制)
    /// </summary>
    /// <param name="videoId"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    private async Task<bool> GetTwitcastingIsPublicAsync(string videoId, CancellationToken cancellation = default)
    {
        var data = await GetTwitcastingInfoDataAsync(videoId, cancellation);
        // 事實上，私人影片會在取得token時失敗，並不會回傳TwitcastingInfoData物件
        return null != data
            && data.Visibility?.Type == "public";
    }

    private async Task<TwitcastingInfoData?> GetTwitcastingInfoDataAsync(string videoId, CancellationToken cancellation = default)
    {
        using var client = _httpFactory.CreateClient();
        int epochTimeStamp = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;

        var token = await GetTwitcastingTokenAsync(videoId, cancellation);
        if (null == token) return null;
        var response = await client.GetAsync($@"{_frontendApi}/movies/{videoId}/info?__n={epochTimeStamp}&token={token}", cancellation);
        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<TwitcastingInfoData>(cancellationToken: cancellation);
        return data;
    }

    /// <summary>
    /// 檢查影片是否發佈
    /// </summary>
    /// <param name="video"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    private async Task<bool> GetTwitcastingIsPublishAsync(Video video, CancellationToken cancellation = default)
    {
        // Web page will contains this string if the video is not published
        var keyword = "tw-player-empty-message";

        using var client = _httpFactory.CreateClient();
        var response = await client.GetAsync($"https://twitcasting.tv/{video.ChannelId}/movie/{video.id}", cancellation);
        var data = await response.Content.ReadAsStringAsync(cancellation);
        return !data.Contains(keyword);
    }

    public override async Task UpdateVideoDataAsync(Video video, CancellationToken cancellation = default)
    {
        _unitOfWork_Public.ReloadEntityFromDB(video);
        if (null == video.Timestamps.ActualStartTime)
        {
            video.Timestamps.ActualStartTime = video.Timestamps.PublishedAt;
        }

        if (string.IsNullOrEmpty(video.Thumbnail))
        {
            video.Thumbnail = await DownloadThumbnailAsync($"https://twitcasting.tv/{video.ChannelId}/thumb/{video.id}", video.id, cancellation);
        }

        if (await GetTwitcastingIsPublishAsync(video, cancellation))
        {
            if (await GetTwitcastingIsPublicAsync(video.id, cancellation))
            {
                var (title, telop) = await GetTwitcastingStreamTitleAsync(video.id, cancellation);
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
                    video.Note = $"Video source is detected access required.";
                    if (null != discordService)
                    {
                        await discordService.SendDeletedMessage(video);
                    }
                    _logger.LogInformation("Video source is {status} because it is detected access required.", Enum.GetName(typeof(VideoStatus), video.SourceStatus));
                }
                video.SourceStatus = VideoStatus.Reject;
            }
        }
        else
        {
            if (video.SourceStatus != VideoStatus.Deleted
               && video.Status == VideoStatus.Archived)
            {
                video.SourceStatus = VideoStatus.Deleted;
                if (null != discordService)
                {
                    // First detected
                    await discordService.SendDeletedMessage(video);
                }
            }
            video.SourceStatus = VideoStatus.Deleted;
            video.Status = VideoStatus.Missing;
            video.Note = "Video is not published.";
            _logger.LogInformation("Twitcasting video {videoId} is not published.", video.id);
        }

        if (await _storageService.IsVideoFileExists(video.Filename, cancellation))
        {
            video.Status = VideoStatus.Archived;
            video.Note = null;
        }
        else if (video.Status == VideoStatus.Archived)
        {
            video.Status = VideoStatus.Expired;
            video.Note = $"Video expired because archived not found.";
            _logger.LogInformation("Can not found archived, change video status to {status}", Enum.GetName(typeof(VideoStatus), VideoStatus.Expired));
        }

        _videoRepository.Update(video);
        _unitOfWork_Public.Commit();
    }

    public override Task UpdateChannelDataAsync(Channel channel, CancellationToken stoppingToken)
        => throw new InvalidOperationException();
}
