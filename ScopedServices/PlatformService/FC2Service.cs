﻿using LivestreamRecorder.DB.Core;
using LivestreamRecorder.DB.Enum;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Interfaces.Job;
using LivestreamRecorderService.Models;
using LivestreamRecorderService.Models.OptionDiscords;
using LivestreamRecorderService.SingletonServices;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Serilog.Context;

namespace LivestreamRecorderService.ScopedServices.PlatformService;

public class FC2Service : PlatformService, IPlatformService
{
    private readonly ILogger<FC2Service> _logger;
    private readonly IUnitOfWork _unitOfWork_Public;
    private readonly IVideoRepository _videoRepository;
    private readonly IChannelRepository _channelRepository;
    private readonly IFC2LiveDLService _fC2LiveDLService;
    private readonly IStorageService _storageService;
    private readonly IHttpClientFactory _httpFactory;

    public override string PlatformName => "FC2";

    public override int Interval => 10;

    private const string _memberApi = "https://live.fc2.com/api/memberApi.php";

    public FC2Service(
        ILogger<FC2Service> logger,
        UnitOfWork_Public unitOfWork_Public,
        IVideoRepository videoRepository,
        IChannelRepository channelRepository,
        IFC2LiveDLService fC2LiveDLService,
        IStorageService storageService,
        DiscordService discordService,
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
        _channelRepository = channelRepository;
        _fC2LiveDLService = fC2LiveDLService;
        _storageService = storageService;
        _httpFactory = httpClientFactory;
    }

    public override async Task UpdateVideosDataAsync(Channel channel, CancellationToken cancellation = default)
    {
        using var ____ = LogContext.PushProperty("Platform", PlatformName);
        using var __ = LogContext.PushProperty("channelId", channel.id);

        _logger.LogTrace("Start to get FC2 stream: {channelId}", channel.id);

        var (isLive, videoId) = await GetFC2LiveStatusAsync(channel, cancellation);
        using var ___ = LogContext.PushProperty("videoId", videoId);

        if (!isLive || string.IsNullOrEmpty(videoId))
        {
            _logger.LogTrace("{channelId} is down.", channel.id);
            return;
        }
        else if (!string.IsNullOrEmpty(videoId))
        {
            Video video;

            if (_videoRepository.Exists(videoId))
            {

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
                        _logger.LogWarning("{videoId} has already been archived. It is possible that an internet disconnect occurred during the process. Changed its state back to Recording.", video.id);
                        video.Status = VideoStatus.WaitingToRecord;
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

            var info = await GetFC2InfoDataAsync(channel.id, cancellation);
            if (null == info) return;

            video.Thumbnail = await DownloadThumbnailAsync(info.Data.ChannelData.Image, video.id, cancellation);
            video.Title = info.Data.ChannelData.Title;
            video.Description = info.Data.ChannelData.Info;
            video.Timestamps.ActualStartTime =
                0 == info.Data.ChannelData.Start
                || null == info.Data.ChannelData.Start
                    ? DateTime.UtcNow
                    : DateTimeOffset.FromUnixTimeMilliseconds((long)info.Data.ChannelData.Start).UtcDateTime;

            if (info.Data.ChannelData.IsLimited == 0 || info.Data.ChannelData.IsPremium == 0)
            {
                if (isLive && (video.Status < VideoStatus.Recording
                               || video.Status == VideoStatus.Missing))
                {
                    _ = _fC2LiveDLService.InitJobAsync(url: $"https://live.fc2.com/{channel.id}/",
                                                                channelId: video.ChannelId,
                                                                useCookiesFile: channel.UseCookiesFile == true,
                                                                cancellation: cancellation);

                    video.Status = VideoStatus.Recording;
                    _logger.LogInformation("{channelId} is now lived! Start recording.", channel.id);
                    _logger.LogDebug("fc2Info: {info}", JsonConvert.SerializeObject(info));
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

    private async Task<(bool Live, string? Id)> GetFC2LiveStatusAsync(Channel channel, CancellationToken cancellation = default)
    {
        var info = await GetFC2InfoDataAsync(channel.id, cancellation);

        var start = info?.Data.ChannelData.Start?.ToString();

        return null == info || string.IsNullOrEmpty(start) || start == "0"
                ? (false, null)
                : (info.Data.ChannelData.IsPublish == 1, start);
    }

    private async Task<FC2MemberData?> GetFC2InfoDataAsync(string channelId, CancellationToken cancellation = default)
    {
        try
        {
            using var client = _httpFactory.CreateClient();
            var response = await client.PostAsync(
                requestUri: $@"{_memberApi}",
                content: new FormUrlEncodedContent(
                    new Dictionary<string, string>()
                    {
                        { "channel", "1" },
                        { "profile", "1" },
                        { "user", "0" },
                        { "streamid", channelId }
                    }),
                cancellationToken: cancellation);
            response.EnsureSuccessStatusCode();
            string jsonString = await response.Content.ReadAsStringAsync(cancellation);
            FC2MemberData? info = JsonConvert.DeserializeObject<FC2MemberData>(jsonString);

            return info;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Get fc2 info failed. {channelId} Be careful if this happens repeatedly.", channelId);
            return null;
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
            if (video.id.StartsWith("20"))
            {
                YtdlpVideoData? videoData = await GetVideoInfoByYtdlpAsync($"https://video.fc2.com/content/{video.id}", cancellation);
                if (null != videoData)
                    video.Thumbnail = await DownloadThumbnailAsync(videoData.Thumbnail, video.id, cancellation);
            }
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

    public override async Task UpdateChannelDataAsync(Channel channel, CancellationToken stoppingToken)
    {
        var avatarBlobUrl = channel.Avatar;
        var info = await GetFC2InfoDataAsync(channel.id, stoppingToken);
        if (null == info)
        {
            _logger.LogWarning("Failed to get channel info for {channelId}", channel.id);
            return;
        }

        var avatarUrl = info.Data.ProfileData.Image;
        if (!string.IsNullOrEmpty(avatarUrl))
        {
            avatarBlobUrl = await DownloadImageAndUploadToBlobStorage(avatarUrl, $"avatar/{channel.id}", stoppingToken);
        }

        _unitOfWork_Public.Context.Entry(channel).Reload();
        channel = _channelRepository.LoadRelatedData(channel);
        channel.ChannelName = info.Data.ProfileData.Name;
        channel.Avatar = avatarBlobUrl?.Replace("avatar/", "");
        _channelRepository.Update(channel);
        _unitOfWork_Public.Commit();
    }
}
