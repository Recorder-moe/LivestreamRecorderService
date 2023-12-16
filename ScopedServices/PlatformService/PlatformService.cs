using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Models;
using LivestreamRecorderService.Models.OptionDiscords;
using LivestreamRecorderService.SingletonServices;
using Microsoft.Extensions.Options;
using MimeMapping;
using System.Configuration;
using YoutubeDLSharp.Options;

namespace LivestreamRecorderService.ScopedServices.PlatformService;

public abstract class PlatformService : IPlatformService
{
    protected readonly IChannelRepository channelRepository;
    protected readonly IStorageService storageService;
    protected readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<PlatformService> _logger;

    public abstract string PlatformName { get; }
    public abstract int Interval { get; }

    private static readonly Dictionary<string, int> _elapsedTime = [];
    protected readonly DiscordService? discordService = null;

    private string _ffmpegPath = "/usr/bin/ffmpeg";
    private string _ytdlPath = "/usr/bin/yt-dlp";

    public PlatformService(
        IChannelRepository channelRepository,
        IStorageService storageService,
        IHttpClientFactory httpClientFactory,
        ILogger<PlatformService> logger,
        IOptions<DiscordOption> discordOptions,
        IServiceProvider serviceProvider)
    {
        this.channelRepository = channelRepository;
        this.storageService = storageService;
        this.httpClientFactory = httpClientFactory;
        _logger = logger;
        _elapsedTime.TryAdd(PlatformName, 0);
        if (discordOptions.Value.Enabled)
            discordService = serviceProvider.GetRequiredService<DiscordService>();
    }

    public async Task<List<Channel>> GetMonitoringChannels()
        => (await channelRepository.GetChannelsBySourceAsync(PlatformName))
                                    .Where(p => p.Monitoring)
                                    .ToList();

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
        if (_elapsedTime[PlatformName] >= Interval)
        {
            _elapsedTime[PlatformName] = 0;
        }
        return false;
    }

    public async Task<YtdlpVideoData?> GetVideoInfoByYtdlpAsync(string url, CancellationToken cancellation = default)
    {
        if (!File.Exists(_ytdlPath) || !File.Exists(_ffmpegPath))
        {
            (string? YtdlPath, string? FFmpegPath) = YoutubeDL.WhereIs();
            _ytdlPath = YtdlPath ?? throw new ConfigurationErrorsException("Yt-dlp is missing.");
            _ffmpegPath = FFmpegPath ?? throw new ConfigurationErrorsException("FFmpeg is missing.");
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
            var res = await ytdl.RunVideoDataFetch_Alt(url, overrideOptions: optionSet, ct: cancellation);
            if (!res.Success)
            {
                throw new Exception(string.Join(' ', res.ErrorOutput));
            }

            YtdlpVideoData videoData = res.Data;
            return videoData;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An exception occurred while getting video info by yt-dlp: {url}", url);
            return null;
        }
    }

    public async Task<string[]?> GetVideoIdsByYtdlpAsync(string url, int limit = 50, CancellationToken cancellation = default)
    {
        if (!File.Exists(_ytdlPath) || !File.Exists(_ffmpegPath))
        {
            (string? YtdlPath, _) = YoutubeDL.WhereIs();
            _ytdlPath = YtdlPath ?? throw new ConfigurationErrorsException("Yt-dlp is missing.");
        }
        var ytdl = new YoutubeDLSharp.YoutubeDL
        {
            YoutubeDLPath = _ytdlPath,
        };

        OptionSet optionSet = new();
        optionSet.AddCustomOption("--ignore-no-formats-error", true);
        optionSet.IgnoreErrors = true;
        optionSet.FlatPlaylist = true;
        optionSet.AddCustomOption("--print", "id");
        if (limit > 0) optionSet.PlaylistItems = $"1:{limit}";

        try
        {
            var res = await ytdl.RunWithOptions(new string[] { url }, optionSet, ct: cancellation);
            if (!res.Success)
            {
                throw new Exception(string.Join(' ', res.ErrorOutput));
            }

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
    /// Download thumbnail.
    /// </summary>
    /// <param name="thumbnail">Url to download the thumbnail.</param>
    /// <param name="videoId"></param>
    /// <param name="cancellation"></param>
    /// <returns>Thumbnail file name with extension.</returns>
    protected async Task<string?> DownloadThumbnailAsync(string thumbnail, string videoId, CancellationToken cancellation = default)
        => string.IsNullOrEmpty(thumbnail)
            ? null
            : (await DownloadImageAndUploadToBlobStorageAsync(thumbnail, $"thumbnails/{videoId}", cancellation))?.Replace("thumbnails/", "");

    /// <summary>
    /// Download image and upload it to Blob Storage
    /// </summary>
    /// <param name="url">Image source url to download.</param>
    /// <param name="path">Path in Blob storage (without extension)</param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    protected async Task<string?> DownloadImageAndUploadToBlobStorageAsync(string url, string path, CancellationToken cancellation)
    {
        if (string.IsNullOrEmpty(url))
        {
            throw new ArgumentNullException(nameof(url));
        }

        if (string.IsNullOrEmpty(path))
        {
            throw new ArgumentNullException(nameof(path));
        }

        string? extension, contentType, pathInStorage, tempPath;
        try
        {
            using var client = httpClientFactory.CreateClient();
            var response = await client.GetAsync(url, cancellation);
            response.EnsureSuccessStatusCode();

            contentType = response.Content.Headers.ContentType?.MediaType;
            extension = MimeUtility.GetExtensions(contentType)?.FirstOrDefault();
            extension = extension == "jpeg" ? "jpg" : extension;
            pathInStorage = $"{path}.{extension}";

            tempPath = Path.GetTempFileName();
            tempPath = Path.ChangeExtension(tempPath, extension);
            using var contentStream = await response.Content.ReadAsStreamAsync(cancellation);
            using var fileStream = new FileStream(tempPath, FileMode.Create);
            await contentStream.CopyToAsync(fileStream, cancellation);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An exception occurred while downloading image: {url}", url);
            return null;
        }

        try
        {
            List<Task> tasks =
            [
                storageService.UploadPublicFileAsync(contentType, pathInStorage, tempPath, cancellation),
                storageService.UploadPublicFileAsync(KnownMimeTypes.Avif, $"{path}.avif", await ImageHelper.ConvertToAvifAsync(tempPath), cancellation)
            ];

            await Task.WhenAll(tasks);
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
            catch (IOException) { }
        }
    }

    public abstract Task UpdateChannelDataAsync(Channel channel, CancellationToken stoppingToken);
}
