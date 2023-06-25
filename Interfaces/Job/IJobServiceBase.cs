using LivestreamRecorder.DB.Models;

namespace LivestreamRecorderService.Interfaces.Job;

public interface IJobServiceBase
{
    /// <summary>
    /// [a-z0-9]([-a-z0-9]*[a-z0-9])?
    /// </summary>
    string Name { get; }

    string GetInstanceName(string videoId);
    Task InitJobAsync(string url, Video video, bool useCookiesFile = false, CancellationToken cancellation = default);
}