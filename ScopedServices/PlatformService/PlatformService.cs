﻿using System.Configuration;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Models;
using LivestreamRecorderService.Models.Options;
using LivestreamRecorderService.SingletonServices;
using Microsoft.Extensions.Options;
using MimeMapping;
using YoutubeDLSharp;
using YoutubeDLSharp.Options;
using YoutubeDL = LivestreamRecorderService.Helper.YoutubeDL;

namespace LivestreamRecorderService.ScopedServices.PlatformService;

public abstract class PlatformService : IPlatformService
{
    private static readonly Dictionary<string, int> _elapsedTime = [];
    private readonly ILogger<PlatformService> _logger;
    protected readonly IChannelRepository ChannelRepository;
    protected readonly DiscordService? DiscordService;
    protected readonly IHttpClientFactory HttpClientFactory;
    protected readonly IStorageService StorageService;

    private string _ffmpegPath = "/usr/local/bin/ffmpeg";
    private string _ytdlPath = "/venv/bin/yt-dlp";

    protected PlatformService(
        IChannelRepository channelRepository,
        IStorageService storageService,
        IHttpClientFactory httpClientFactory,
        ILogger<PlatformService> logger,
        IOptions<DiscordOption> discordOptions,
        IServiceProvider serviceProvider)
    {
        ChannelRepository = channelRepository;
        StorageService = storageService;
        HttpClientFactory = httpClientFactory;
        _logger = logger;
        // ReSharper disable once VirtualMemberCallInConstructor
        _elapsedTime.TryAdd(PlatformName, 0);
        if (discordOptions.Value.Enabled)
            DiscordService = serviceProvider.GetRequiredService<DiscordService>();
    }

    public abstract string PlatformName { get; }
    public abstract int Interval { get; }

    public Task<List<Channel>> GetAllChannels()
    {
        return ChannelRepository.GetChannelsBySourceAsync(PlatformName);
    }

    public async Task<List<Channel>> GetMonitoringChannels()
    {
        return (await GetAllChannels())
               .Where(p => p.Monitoring)
               .ToList();
    }

    public abstract Task UpdateVideosDataAsync(Channel channel, CancellationToken cancellation = default);

    public abstract Task UpdateVideoDataAsync(Video video, CancellationToken cancellation = default);

    public bool StepInterval(int elapsedTime)
    {
        if (_elapsedTime[PlatformName] == 0)
        {
            _elapsedTime[PlatformName] += elapsedTime;
            return true;
        }

        _elapsedTime[PlatformName] += elapsedTime;
        if (_elapsedTime[PlatformName] >= Interval) _elapsedTime[PlatformName] = 0;

        return false;
    }

    public async Task<YtdlpVideoData?> GetVideoInfoByYtdlpAsync(string url, CancellationToken cancellation = default)
    {
        if (!File.Exists(_ytdlPath) || !File.Exists(_ffmpegPath))
        {
            (string? ytdlPath, string? fFmpegPath) = YoutubeDL.WhereIs();
            _ytdlPath = ytdlPath ?? throw new ConfigurationErrorsException("Yt-dlp is missing.");
            _ffmpegPath = fFmpegPath ?? throw new ConfigurationErrorsException("FFmpeg is missing.");
        }

        var ytdl = new YoutubeDLSharp.YoutubeDL
        {
            YoutubeDLPath = _ytdlPath,
            FFmpegPath = _ffmpegPath
        };

        OptionSet optionSet = new();
        optionSet.AddCustomOption("--ignore-no-formats-error", true);

        try
        {
            RunResult<YtdlpVideoData>? res = await ytdl.RunVideoDataFetch_Alt(url, overrideOptions: optionSet, ct: cancellation);
            if (!res.Success)
                throw new InvalidOperationException(
                    $"Failed to fetch video data from yt-dlp for URL: {url}. Errors: {string.Join(' ', res.ErrorOutput)}");

            YtdlpVideoData? videoData = res.Data;
            return videoData;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An exception occurred while getting video info by yt-dlp: {url}", url);
            return null;
        }
    }

    public abstract Task UpdateChannelDataAsync(Channel channel, CancellationToken stoppingToken);

    protected async Task<string[]?> GetVideoIdsByYtdlpAsync(string url, int limit = 50, CancellationToken cancellation = default)
    {
        if (!File.Exists(_ytdlPath) || !File.Exists(_ffmpegPath))
        {
            (string? ytdlPath, _) = YoutubeDL.WhereIs();
            _ytdlPath = ytdlPath ?? throw new ConfigurationErrorsException("Yt-dlp is missing.");
        }

        var ytdl = new YoutubeDLSharp.YoutubeDL
        {
            YoutubeDLPath = _ytdlPath
        };

        OptionSet optionSet = new();
        optionSet.AddCustomOption("--ignore-no-formats-error", true);
        optionSet.IgnoreErrors = true;
        optionSet.FlatPlaylist = true;
        optionSet.AddCustomOption("--print", "id");
        if (limit > 0) optionSet.PlaylistItems = $"1:{limit}";

        try
        {
            RunResult<string[]>? res = await ytdl.RunWithOptions([url], optionSet, ct: cancellation);
            if (!res.Success)
                throw new InvalidOperationException(
                    $"Failed to fetch video data from yt-dlp for URL: {url}. Errors: {string.Join(' ', res.ErrorOutput)}");

            string[] videoIds = res.Data;
            return videoIds;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An exception occurred while getting video info by yt-dlp: {url}", url);
            return null;
        }
    }

    /// <summary>
    ///     Download thumbnail.
    /// </summary>
    /// <param name="thumbnail">Url to download the thumbnail.</param>
    /// <param name="videoId"></param>
    /// <param name="cancellation"></param>
    /// <returns>Thumbnail file name with extension.</returns>
    protected async Task<string?> DownloadThumbnailAsync(string thumbnail, string videoId, CancellationToken cancellation = default)
    {
        return string.IsNullOrEmpty(thumbnail)
            ? null
            : (await DownloadImageAndUploadToBlobStorageAsync(thumbnail, $"thumbnails/{videoId}", cancellation))?.Replace("thumbnails/", "");
    }

    /// <summary>
    ///     Download image and upload it to Blob Storage
    /// </summary>
    /// <param name="url">Image source url to download.</param>
    /// <param name="path">Path in Blob storage (without extension)</param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    protected async Task<string?> DownloadImageAndUploadToBlobStorageAsync(string url, string path, CancellationToken cancellation)
    {
        if (string.IsNullOrEmpty(url)) throw new ArgumentNullException(nameof(url));

        if (!url.StartsWith("http")) url = "https:" + url;

        if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

        string? contentType, pathInStorage, tempPath;
        try
        {
            using HttpClient client = HttpClientFactory.CreateClient();
            HttpResponseMessage response = await client.GetAsync(url, cancellation);
            response.EnsureSuccessStatusCode();

            contentType = response.Content.Headers.ContentType?.MediaType;
            string? extension = MimeUtility.GetExtensions(contentType)?.FirstOrDefault();
            extension = extension == "jpeg" ? "jpg" : extension;
            pathInStorage = $"{path}.{extension}";

            tempPath = Path.GetTempFileName();
            tempPath = Path.ChangeExtension(tempPath, extension);
            await using Stream contentStream = await response.Content.ReadAsStreamAsync(cancellation);
            await using var fileStream = new FileStream(tempPath, FileMode.Create);
            await contentStream.CopyToAsync(fileStream, cancellation);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An exception occurred while downloading image: {url}", url);
            return null;
        }

        try
        {
            await StorageService.UploadPublicFileAsync(contentType: contentType,
                                                       pathInStorage: pathInStorage,
                                                       filePathToUpload: tempPath,
                                                       cancellation: cancellation);

            await StorageService.UploadPublicFileAsync(contentType: KnownMimeTypes.Avif,
                                                       pathInStorage: $"{path}.avif",
                                                       filePathToUpload: await ImageHelper.ConvertToAvifAsync(tempPath),
                                                       cancellation: cancellation);

            return pathInStorage;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An exception occurred while uploading image to blob storage: {url}", url);
            return null;
        }
        finally
        {
            try
            {
                File.Delete(tempPath);
                File.Delete(Path.ChangeExtension(tempPath, ".avif"));
            }
            catch (IOException)
            {
                // ignored
            }
        }
    }
}
