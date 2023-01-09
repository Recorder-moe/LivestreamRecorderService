using LivestreamRecorderService.DB.Models;

namespace LivestreamRecorderService.Interfaces;

public interface IPlatformSerivce
{
    public string PlatformName { get; }
    List<Channel> GetMonitoringChannels();
    Task UpdateVideosDataAsync(Channel channel);
}