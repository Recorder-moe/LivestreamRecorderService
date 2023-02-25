using Azure.Storage.Blobs.Models;
using LivestreamRecorderService.DB.Interfaces;
using LivestreamRecorderService.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Models;
using MimeMapping;
using System.Configuration;
using YoutubeDLSharp.Options;

namespace LivestreamRecorderService.ScopedServices
{
    public abstract class PlatformService : IPlatformSerivce
    {
        private readonly IChannelRepository _channelRepository;
        private readonly IABSService _aBSService;
        private readonly IHttpClientFactory _httpFactory;
        private readonly ILogger<PlatformService> _logger;

        public abstract string PlatformName { get; }
        public abstract int Interval { get; }

        private static readonly Dictionary<string, int> _elapsedTime = new();

        private string _ffmpegPath = "/usr/bin/ffmpeg";
        private string _ytdlPath = "/usr/bin/yt-dlp";

        public PlatformService(
            IChannelRepository channelRepository,
            IABSService aBSService,
            IHttpClientFactory httpClientFactory,
            ILogger<PlatformService> logger)
        {
            _channelRepository = channelRepository;
            _aBSService = aBSService;
            _httpFactory = httpClientFactory;
            _logger = logger;
            if (!_elapsedTime.ContainsKey(PlatformName))
            {
                _elapsedTime.Add(PlatformName, 0);
            }
        }

        public List<Channel> GetMonitoringChannels()
            => _channelRepository.GetMonitoringChannels()
                                 .Where(p => p.Source == PlatformName)
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
                : (await DownloadImageAndUploadToBlobStorage(thumbnail, $"thumbnails/{videoId}", cancellation))?.Replace("thumbnails/", "");

        /// <summary>
        /// Download image and upload it to Blob Storage
        /// </summary>
        /// <param name="url">Image source url to download.</param>
        /// <param name="path">Path in Blob storage (without extension)</param>
        /// <param name="cancellation"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        protected async Task<string?> DownloadImageAndUploadToBlobStorage(string url, string path, CancellationToken cancellation)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentNullException(nameof(url));
            }

            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            using var client = _httpFactory.CreateClient();
            var response = await client.GetAsync(url, cancellation);
            if (response.IsSuccessStatusCode)
            {
                var contentType = response.Content.Headers.ContentType?.MediaType;
                var extension = MimeUtility.GetExtensions(contentType)?.FirstOrDefault();
                extension = extension == "jpeg" ? "jpg" : extension;
                var blobClient = _aBSService.GetPublicBlob($"{path}.{extension}");
                _ = await blobClient.UploadAsync(
                     content: await response.Content.ReadAsStreamAsync(cancellation),
                     httpHeaders: new BlobHttpHeaders { ContentType = contentType },
                     accessTier: AccessTier.Hot,
                     cancellationToken: cancellation);
                return $"{path}.{extension}";
            }
            return null;
        }
    }
}
