using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Models;

namespace LivestreamRecorderService.Interfaces;

public interface IPlatformService
{
    public string PlatformName { get; }
    Task UpdateVideosDataAsync(Channel channel, CancellationToken cancellation = default);
    Task UpdateVideoDataAsync(Video video, CancellationToken cancellation = default);

    /// <summary>
    /// Step interval and return true if interval is reached
    /// </summary>
    /// <param name="elapsedTime"></param>
    /// <returns>Should trigger</returns>
    public bool StepInterval(int elapsedTime);
    Task<YtdlpVideoData?> GetVideoInfoByYtdlpAsync(string url, CancellationToken cancellation = default);
    Task UpdateChannelDataAsync(Channel channel, CancellationToken stoppingToken);
    Task<List<Channel>> GetMonitoringChannels();

    /// <summary>
    /// 每幾秒執行一次
    /// </summary>
    public int Interval { get; }
}