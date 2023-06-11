using FileInfo = LivestreamRecorderService.Models.FileInfo;

namespace LivestreamRecorderService.Interfaces;

public interface ISharedVolumeService
{
    Task<FileInfo?> GetVideoFileInfoByPrefixAsync(string prefix, TimeSpan delay, CancellationToken cancellation = default);
}