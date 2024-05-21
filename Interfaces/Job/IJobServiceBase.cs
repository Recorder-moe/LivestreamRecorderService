using LivestreamRecorder.DB.Models;

namespace LivestreamRecorderService.Interfaces.Job;

public interface IJobServiceBase
{
    /// <summary>
    ///     [a-z0-9]([-a-z0-9]*[a-z0-9])?
    /// </summary>
    string Name { get; }

    string GetInstanceName(string videoId);

    Task CreateJobAsync(Video video,
                        bool useCookiesFile = false,
                        string? url = null,
                        CancellationToken cancellation = default);
}
