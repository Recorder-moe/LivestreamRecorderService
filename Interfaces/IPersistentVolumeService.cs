using FileInfo = LivestreamRecorderService.Models.FileInfo;

namespace LivestreamRecorderService.Interfaces;

public interface IPersistentVolumeService
{
    Task<FileInfo?> GetVideoFileInfoByPrefixAsync(string prefix, TimeSpan delay, CancellationToken cancellation = default);
}