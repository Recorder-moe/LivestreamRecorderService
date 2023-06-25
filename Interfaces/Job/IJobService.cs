using LivestreamRecorder.DB.Models;

namespace LivestreamRecorderService.Interfaces.Job;

public interface IJobService
{
    Task<bool> IsJobFailedAsync(Video video, CancellationToken cancellation = default);
    Task<bool> IsJobFailedAsync(string keyword, CancellationToken cancellation = default);
    Task<bool> IsJobSucceededAsync(Video video, CancellationToken cancellation = default);
    Task<bool> IsJobSucceededAsync(string keyword, CancellationToken cancellation = default);
    Task RemoveCompletedJobsAsync(Video video, CancellationToken cancellation = default);
}
