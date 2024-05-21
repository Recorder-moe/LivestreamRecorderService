using LivestreamRecorder.DB.Models;

namespace LivestreamRecorderService.Interfaces;

public interface IJobService
{
    Task<bool> IsJobFailedAsync(Video video, CancellationToken cancellation = default);
    Task<bool> IsJobFailedAsync(string keyword, CancellationToken cancellation = default);
    Task<bool> IsJobSucceededAsync(Video video, CancellationToken cancellation = default);
    Task<bool> IsJobSucceededAsync(string keyword, CancellationToken cancellation = default);
    Task RemoveCompletedJobsAsync(Video video, CancellationToken cancellation = default);

    /// <summary>
    ///     Create Instance. Cookies file will be mounted at /cookies if K8s secrets exists.
    /// </summary>
    /// <param name="deploymentName">
    ///     Should be unique. Must consist of lower case alphanumeric characters or '-', and must
    ///     start and end with an alphanumeric character (e.g. 'my-name',  or '123-abc', regex used for validation is
    ///     '[a-z0-9]([-a-z0-9]*[a-z0-9])?')
    /// </param>
    /// <param name="containerName">
    ///     Should be unique. Must consist of lower case alphanumeric characters or '-', and must start
    ///     and end with an alphanumeric character (e.g. 'my-name',  or '123-abc', regex used for validation is
    ///     '[a-z0-9]([-a-z0-9]*[a-z0-9])?')
    /// </param>
    /// <param name="imageName">Download container image with tag. (e.g. 'yt-dlp:latest')</param>
    /// <param name="fileName">Recording file name.</param>
    /// <param name="command">
    ///     Override command for the download container. The original ENTRYPOINT will be used if not
    ///     provided.
    /// </param>
    /// <param name="args">
    ///     Override args for the download container. The original CMD will be used if not provided. Both
    ///     <paramref name="command" /> and <paramref name="args" /> cannot be empty at the same time.
    /// </param>
    /// <param name="mountPath">The mount path of the download container.</param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    Task CreateInstanceAsync(string deploymentName,
                             string containerName,
                             string imageName,
                             string fileName,
                             string[]? command = null,
                             string[]? args = null,
                             string mountPath = "/sharedvolume",
                             CancellationToken cancellation = default);
}
