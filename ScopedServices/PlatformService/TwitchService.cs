using LivestreamRecorder.DB.Core;
using LivestreamRecorder.DB.Enum;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Interfaces.Job;
using LivestreamRecorderService.Models.OptionDiscords;
using Microsoft.Extensions.Options;
using Serilog.Context;
using TwitchLib.Api.Interfaces;

namespace LivestreamRecorderService.ScopedServices.PlatformService;

public class TwitchService : PlatformService, IPlatformService
{
    private readonly ILogger<TwitchService> _logger;
    private readonly IUnitOfWork _unitOfWork_Public;
    private readonly IVideoRepository _videoRepository;
    private readonly IStreamlinkService _streamlinkService;
    private readonly ITwitchAPI _twitchAPI;
    private readonly IStorageService _storageService;

    public override string PlatformName => "Twitch";

    public override int Interval => 60;

    public TwitchService(
        ILogger<TwitchService> logger,
        UnitOfWork_Public unitOfWork_Public,
        IVideoRepository videoRepository,
        IChannelRepository channelRepository,
        IStreamlinkService streamlinkService,
        ITwitchAPI twitchAPI,
        IStorageService storageService,
        IHttpClientFactory httpClientFactory,
        IOptions<DiscordOption> discordOptions,
        IServiceProvider serviceProvider) : base(channelRepository,
                                                 storageService,
                                                 httpClientFactory,
                                                 logger,
                                                 discordOptions,
                                                 serviceProvider)
    {
        _logger = logger;
        _unitOfWork_Public = unitOfWork_Public;
        _videoRepository = videoRepository;
        _streamlinkService = streamlinkService;
        _twitchAPI = twitchAPI;
        _storageService = storageService;
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
                _ = _streamlinkService.InitJobAsync(url: video.id,
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

        if (!await _storageService.IsVideoFileExists(video.Filename, cancellation))
        {
            if (video.Status >= VideoStatus.Archived && video.Status < VideoStatus.Expired)
            {
                video.Status = VideoStatus.Missing;
                video.Note = $"Video missing because archived not found.";
                _logger.LogInformation("Can not found archived, change video status to {status}", Enum.GetName(typeof(VideoStatus), video.Status));
            }
        }
        else if (video.Status < VideoStatus.Archived || video.Status >= VideoStatus.Expired)
        {
            video.Status = VideoStatus.Archived;
            video.Note = null;
            _logger.LogInformation("Correct video status to {status} because archived is exists.", Enum.GetName(typeof(VideoStatus), video.Status));
        }

        _videoRepository.Update(video);
        _unitOfWork_Public.Commit();
    }

    public override Task UpdateChannelDataAsync(Channel channel, CancellationToken stoppingToken)
        => throw new InvalidOperationException();
}
