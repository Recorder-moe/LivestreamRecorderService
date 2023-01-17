using LivestreamRecorderService.DB.Enum;
using LivestreamRecorderService.DB.Interfaces;
using LivestreamRecorderService.DB.Models;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.SingletonServices;
using Serilog.Context;
using TwitchLib.Api.Interfaces;

namespace LivestreamRecorderService.ScopedServices;

public class TwitchSerivce : PlatformService, IPlatformSerivce
{
    private readonly ILogger<TwitchSerivce> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IVideoRepository _videoRepository;
    private readonly ACIStreamlinkService _aCIStreamlinkService;
    private readonly ITwitchAPI _twitchAPI;
    private readonly IABSService _aBSService;

    public override string PlatformName => "Twitch";

    public override int Interval => 60;

    public TwitchSerivce(
        ILogger<TwitchSerivce> logger,
        IUnitOfWork unitOfWork,
        IVideoRepository videoRepository,
        IChannelRepository channelRepository,
        ACIStreamlinkService aCIStreamlinkService,
        ITwitchAPI twitchAPI,
        IABSService aBSService,
        IHttpClientFactory httpClientFactory) : base(channelRepository, aBSService, httpClientFactory)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _videoRepository = videoRepository;
        _aCIStreamlinkService = aCIStreamlinkService;
        _twitchAPI = twitchAPI;
        _aBSService = aBSService;
    }

    public override async Task UpdateVideosDataAsync(Channel channel, CancellationToken cancellation = default)
    {
        using var _ = LogContext.PushProperty("Platform", PlatformName);
        using var __ = LogContext.PushProperty("channelId", channel.id);

        _logger.LogTrace("Start to get Twitch stream: {channelId}", channel.id);
        var streams = await _twitchAPI.Helix.Streams.GetStreamsAsync(userLogins: new() { channel.id });
        if (null != streams
            && streams.Streams.Length > 0
            && streams.Streams.First() is TwitchLib.Api.Helix.Models.Streams.GetStreams.Stream stream)
        {
            Video video;
            using var ___ = LogContext.PushProperty("videoId", stream.Id);

            if (_videoRepository.Exists(stream.Id))
            {
                video = _videoRepository.GetById(stream.Id);
                if ((video.Status == VideoStatus.Recording
                     || video.Status == VideoStatus.WaitingToRecord)
                    && video.Title == stream.Title
                    && video.Description == stream.GameName)
                {
                    _logger.LogTrace("{channelId} is already recording.", channel.id);
                    return;
                }
            }
            else
            {
                video = new Video()
                {
                    id = stream.Id,
                    Source = PlatformName,
                    Status = VideoStatus.WaitingToRecord,
                    SourceStatus = VideoStatus.Unknown,
                    IsLiveStream = true,
                    Title = stream.Title,
                    Description = stream.GameName,
                    Timestamps = new()
                    {
                        ActualStartTime = stream.StartedAt,
                        PublishedAt = stream.StartedAt,
                    },

                    ChannelId = channel.id,
                    Channel = channel
                };
            }

            video.Title = stream.Title;
            video.Description = stream.GameName;
            video.Timestamps.ActualStartTime = stream.StartedAt;
            video.Timestamps.PublishedAt = stream.StartedAt;

            if (video.Status < VideoStatus.Recording
                || video.Status == VideoStatus.Missing)
            {
                await _aCIStreamlinkService.StartInstanceAsync(userid: video.ChannelId,
                                                               videoId: video.id,
                                                               cancellation: cancellation);
                video.Status = VideoStatus.Recording;
                _logger.LogInformation("{channelId} is now lived! Start recording.", channel.id);
            }

            _videoRepository.AddOrUpdate(video);
            _unitOfWork.Commit();
        }
        else
        {
            _logger.LogTrace("{channelId} is down.", channel.id);
        }
    }

    public override async Task UpdateVideoDataAsync(Video video, CancellationToken cancellation = default)
    {
        if (string.IsNullOrEmpty(video.Thumbnail))
        {
            video.Thumbnail = await DownloadThumbnailAsync($"https://www.twitch.tv/videos/{video.id}", video.id, cancellation);
        }

        if (await GetTwitchIsPublishAsync(video, cancellation))
        {
            video.SourceStatus = VideoStatus.Exist;
            //video.Status = VideoStatus.WaitingToDownload;
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
        else if (video.Status == VideoStatus.Archived)
        {
            video.Status = VideoStatus.Expired;
            _logger.LogInformation("Can not found archived, change video status to {status}", Enum.GetName(typeof(VideoStatus), VideoStatus.Expired));
        }

        _videoRepository.Update(video);
        _unitOfWork.Commit();
    }

    // TODO: Detect if Twitch VOD has been deleted.
    private Task<bool> GetTwitchIsPublishAsync(Video video, CancellationToken cancellation)
        => Task.FromResult(false);
    //=> throw new NotImplementedException();
}
