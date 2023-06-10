namespace LivestreamRecorderService.Interfaces.Job;

public interface IJobServiceBase
{
    string DownloaderName { get; }

    Task<dynamic> InitJobAsync(string url, string channelId, bool useCookiesFile = false, CancellationToken cancellation = default);
}