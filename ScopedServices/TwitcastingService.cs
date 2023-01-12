using LivestreamRecorderService.DB.Enum;
using LivestreamRecorderService.DB.Interfaces;
using LivestreamRecorderService.DB.Models;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Models;
using Serilog.Context;
using System.Net.Http.Json;
using File = LivestreamRecorderService.DB.Models.File;

namespace LivestreamRecorderService.ScopedServices;

public class TwitcastingService : PlatformService, IPlatformSerivce
{
    private readonly ILogger<TwitcastingService> _logger;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IVideoRepository _videoRepository;

    public override string PlatformName => "Twitcasting";
    public override int Interval => 30;

    private const string _streamServerApi = "https://twitcasting.tv/streamserver.php";
    private const string _frontendApi = "https://frontendapi.twitcasting.tv";
    private const string _happytokenApi = "https://twitcasting.tv/happytoken.php";

    public TwitcastingService(
        ILogger<TwitcastingService> logger,
        IHttpClientFactory httpClientFactory,
        IUnitOfWork unitOfWork,
        IVideoRepository videoRepository,
        IChannelRepository channelRepository) : base(channelRepository)
    {
        _logger = logger;
        _httpFactory = httpClientFactory;
        _unitOfWork = unitOfWork;
        _videoRepository = videoRepository;
    }

    public override async Task UpdateVideosDataAsync(Channel channel, CancellationToken cancellation = default)
    {
        using var _ = LogContext.PushProperty("Platform", PlatformName);

        var (isLive, videoId) = await GetTwitcastingLiveStatusAsync(channel, cancellation);

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
                if ((video.Status == VideoStatus.Recording
                     || video.Status == VideoStatus.WaitingToRecord))
                {
                    _logger.LogTrace("{channelId} is already recording.", channel.id);
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
                    IsLiveStream = true,
                    Title = null!,
                    ChannelId = channel.id,
                    Channel = channel,
                    Timestamps = new Timestamps()
                    {
                        PublishedAt = DateTime.UtcNow
                    },
                    Files = new List<File>()
                };
            }

            var (title, telop) = await GetTwitcastingStreamTitleAsync(videoId, cancellation);
            video.Title ??= title ?? "";
            video.Description ??= telop ?? "";

            if (!(await GetTwitcastingIsPublicAsync(videoId, cancellation) ?? false))
            {
                video.Status = VideoStatus.Reject;
                _logger.LogWarning("This video is not public! Skip {videoId}", videoId);
            }

            if (isLive && (video.Status < VideoStatus.Recording
                           || video.Status == VideoStatus.Missing))
            {
                video.Status = VideoStatus.WaitingToRecord;
                _logger.LogInformation("{channelId} is now lived! Start recording.", channel.id);
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
        if (null == token) return default;

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
            _logger.LogError("Failed to get Twitcasting token!");
            _logger.LogError("Failed to get video title! {videoId}", videoId);
        }
        return null;
    }

    private async Task<bool?> GetTwitcastingIsPublicAsync(string videoId, CancellationToken cancellation = default)
        => (await GetTwitcastingInfoDataAsync(videoId, cancellation))?.Visibility?.Type == "public";

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

}
