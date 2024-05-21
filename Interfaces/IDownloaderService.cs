using LivestreamRecorder.DB.Models;

namespace LivestreamRecorderService.Interfaces;

public interface IDownloaderService
{
    Task CreateJobAsync(Video video,
                        bool useCookiesFile = false,
                        string? url = null,
                        CancellationToken cancellation = default);
}

public interface IFc2LiveDLService : IDownloaderService
{
    public const string Name = "fc2livedl";
}

public interface IStreamlinkService : IDownloaderService
{
    public const string Name = "streamlink";
}

public interface ITwitcastingRecorderService : IDownloaderService
{
    public const string Name = "twitcastingrecorder";
}

public interface IYtarchiveService : IDownloaderService
{
    public const string Name = "ytarchive";
}

public interface IYtdlpService : IDownloaderService
{
    public const string Name = "ytdlp";
}
