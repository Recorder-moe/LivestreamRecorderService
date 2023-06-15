using LivestreamRecorder.DB.Models;

namespace LivestreamRecorderService.Interfaces.Job;

public interface IJobServiceBase
{
    string DownloaderName { get; }

    Task<dynamic> InitJobAsync(string url, Video video, bool useCookiesFile = false, CancellationToken cancellation = default);
}