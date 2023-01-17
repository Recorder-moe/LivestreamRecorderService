using LivestreamRecorderService.DB.Enum;
using LivestreamRecorderService.DB.Interfaces;
using LivestreamRecorderService.DB.Models;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Models;
using LivestreamRecorderService.SingletonServices;
using Serilog.Context;
using System.Net.Http.Json;

namespace LivestreamRecorderService.ScopedServices;

public class TwitcastingService : PlatformService, IPlatformSerivce
{
    private readonly ILogger<TwitcastingService> _logger;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IVideoRepository _videoRepository;
    private readonly ACITwitcastingRecorderService _aCITwitcastingRecorderService;
    private readonly IABSService _aBSService;

    public override string PlatformName => "Twitcasting";
    public override int Interval => 10;

    private const string _streamServerApi = "https://twitcasting.tv/streamserver.php";
    private const string _frontendApi = "https://frontendapi.twitcasting.tv";
    private const string _happytokenApi = "https://twitcasting.tv/happytoken.php";

    public TwitcastingService(
        ILogger<TwitcastingService> logger,
        IHttpClientFactory httpClientFactory,
        IUnitOfWork unitOfWork,
        IVideoRepository videoRepository,
        ACITwitcastingRecorderService aCITwitcastingRecorderService,
        IABSService aBSService,
        IChannelRepository channelRepository) : base(channelRepository, aBSService, httpClientFactory)
    {
        _logger = logger;
        _httpFactory = httpClientFactory;
        _unitOfWork = unitOfWork;
        _videoRepository = videoRepository;
        _aCITwitcastingRecorderService = aCITwitcastingRecorderService;
        _aBSService = aBSService;
    }

    public override async Task UpdateVideosDataAsync(Channel channel, CancellationToken cancellation = default)
    {
        using var _ = LogContext.PushProperty("Platform", PlatformName);
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
                        _logger.LogTrace("{videoId} is rejected for recording.", video.id);
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
                        PublishedAt = DateTime.UtcNow
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
                    await _aCITwitcastingRecorderService.StartInstanceAsync(channelId: video.ChannelId,
                                                                            videoId: videoId,
                                                                            cancellation: cancellation);
                    video.Status = VideoStatus.Recording;
                    _logger.LogInformation("{channelId} is now lived! Start recording.", channel.id);
                }
            }
            else
            {
                video.Status = VideoStatus.Reject;
                video.SourceStatus = VideoStatus.Reject;
                _logger.LogWarning("This video is not public! Skip {videoId}", videoId);
            }

            _videoRepository.AddOrUpdate(video);
            _unitOfWork.Commit();
        }
    }

    // Example
    // https://github.com/kkent030315/twitcasting-py/blob/main/src/twitcasting.py
    private async Task<(bool Live, string? Id)> GetTwitcastingLiveStatusAsync(Channel channel, CancellationToken cancellation = default)
    {
        using var client = _httpFactory.CreateClient();
        var response = await client.GetAsync($@"{_streamServerApi}?target={channel.id}&mode=client", cancellation);
        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<TwitcastingStreamData>(cancellationToken: cancellation);

        return null == data
                ? (false, null)
                : (data.Movie.Live ?? false, data.Movie.Id.ToString());
    }

    private async Task<(string? title, string? telop)> GetTwitcastingStreamTitleAsync(string videoId, CancellationToken cancellation = default)
    {
        using var client = _httpFactory.CreateClient();

        var token = await GetTwitcastingTokenAsync(videoId, cancellation);
        if (null == token)
        {
            _logger.LogError("Failed to get video title! {videoId}", videoId);
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
        if (string.IsNullOrEmpty(video.Thumbnail))
        {
            video.Thumbnail = await DownloadThumbnailAsync($"https://twitcasting.tv/{video.ChannelId}/thumb/{video.id}", video.id, cancellation);
        }

        if (await GetTwitcastingIsPublishAsync(video, cancellation))
        {
            video.SourceStatus = VideoStatus.Exist;
            video.Status = VideoStatus.WaitingToDownload;
        }
        else
        {
            video.SourceStatus = VideoStatus.Deleted;
            video.Status = VideoStatus.Missing;
        }

        if (_aBSService.GetBlobByVideo(video, cancellation)
                       .Exists(cancellation))
        {
            video.Status = VideoStatus.Archived;
        }

        _videoRepository.Update(video);
        _unitOfWork.Commit();
    }

}
