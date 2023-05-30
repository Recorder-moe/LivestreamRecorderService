using LivestreamRecorder.DB.Core;
using LivestreamRecorder.DB.Enum;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.SingletonServices;
using Serilog.Context;
using TwitchLib.Api.Interfaces;

namespace LivestreamRecorderService.ScopedServices;

public class TwitchService : PlatformService, IPlatformService
{
    private readonly ILogger<TwitchService> _logger;
    private readonly IUnitOfWork _unitOfWork_Public;
    private readonly IVideoRepository _videoRepository;
    private readonly ACIStreamlinkService _aCIStreamlinkService;
    private readonly ITwitchAPI _twitchAPI;
    private readonly IABSService _aBSService;
    private readonly DiscordService _discordService;

    public override string PlatformName => "Twitch";

    public override int Interval => 60;

    public TwitchService(
        ILogger<TwitchService> logger,
        UnitOfWork_Public unitOfWork_Public,
        IVideoRepository videoRepository,
        IChannelRepository channelRepository,
        ACIStreamlinkService aCIStreamlinkService,
        ITwitchAPI twitchAPI,
        IABSService aBSService,
        DiscordService discordService,
        IHttpClientFactory httpClientFactory) : base(channelRepository, aBSService, httpClientFactory, logger)
    {
        _logger = logger;
        _unitOfWork_Public = unitOfWork_Public;
        _videoRepository = videoRepository;
        _aCIStreamlinkService = aCIStreamlinkService;
        _twitchAPI = twitchAPI;
        _aBSService = aBSService;
        _discordService = discordService;
    }

    public override async Task UpdateVideosDataAsync(Channel channel, CancellationToken cancellation = default)
    {
        using var ____ = LogContext.PushProperty("Platform", PlatformName);
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
                switch (video.Status)
                {
                    case VideoStatus.WaitingToRecord:
                    case VideoStatus.Recording:
                        if (video.Title == stream.Title
                            && video.Description == stream.GameName
                            && null != video.Thumbnail)
                        {
                            _logger.LogTrace("{channelId} is already recording.", channel.id);
                            return;
                        }
                        break;
                    case VideoStatus.Reject:
                    case VideoStatus.Skipped:
                        _logger.LogTrace("{videoId} is rejected for recording.", video.id);
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
                        PublishedAt = stream.StartedAt,
                        ActualStartTime = stream.StartedAt,
                    },

                    ChannelId = channel.id,
                    Channel = channel
                };
            }

            video.Title = stream.Title;
            video.Description = stream.GameName;
            video.Timestamps.ActualStartTime = stream.StartedAt;
            video.Timestamps.PublishedAt = stream.StartedAt;
            video.Thumbnail = await DownloadThumbnailAsync(stream.ThumbnailUrl, video.id, cancellation);

            if (video.Status < VideoStatus.Recording
                || video.Status == VideoStatus.Missing)
            {
                _ = _aCIStreamlinkService.StartInstanceAsync(videoId: video.id,
                                                             channelId: video.ChannelId,
                                                             useCookiesFile: false,
                                                             cancellation: cancellation);

                video.Status = VideoStatus.Recording;
                _logger.LogInformation("{channelId} is now lived! Start recording.", channel.id);
                await _discordService.SendStartRecordingMessage(video);
            }

            _videoRepository.AddOrUpdate(video);
            _unitOfWork_Public.Commit();
        }
        else
        {
            _logger.LogTrace("{channelId} is down.", channel.id);
        }
    }

    public override async Task UpdateVideoDataAsync(Video video, CancellationToken cancellation = default)
    {
        _unitOfWork_Public.ReloadEntityFromDB(video);
        if (null == video.Timestamps.ActualStartTime)
        {
            video.Timestamps.ActualStartTime = video.Timestamps.PublishedAt;
        }

        if (video.Status <= VideoStatus.Pending)
        {
            video.Status = VideoStatus.WaitingToDownload;
        }

        if (await _aBSService.GetVideoBlob(video)
                             .ExistsAsync(cancellation))
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
}
