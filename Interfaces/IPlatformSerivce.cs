using LivestreamRecorderService.DB.Models;

namespace LivestreamRecorderService.Interfaces;

public interface IPlatformSerivce
{
    public string PlatformName { get; }
    List<Channel> GetMonitoringChannels();
    Task UpdateVideosDataAsync(Channel channel);

    /// <summary>
    /// Step interval and return true if interval is reached
    /// </summary>
    /// <param name="elapsedTime"></param>
    /// <returns>Should trigger</returns>
    public bool StepInterval(int elapsedTime);

    /// <summary>
    /// 每幾秒執行一次
    /// </summary>
    public int Interval { get; }
}