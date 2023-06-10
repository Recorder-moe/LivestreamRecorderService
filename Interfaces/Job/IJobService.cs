using LivestreamRecorder.DB.Models;

namespace LivestreamRecorderService.Interfaces.Job;

public interface IJobService
{
    Task<bool> IsJobFailedAsync(Video video, CancellationToken cancellation);
    Task RemoveCompletedJobsAsync(Video video, CancellationToken cancellation = default);
}
