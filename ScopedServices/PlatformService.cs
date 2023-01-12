using LivestreamRecorderService.DB.Interfaces;
using LivestreamRecorderService.DB.Models;
using LivestreamRecorderService.Interfaces;

namespace LivestreamRecorderService.ScopedServices
{
    public abstract class PlatformService : IPlatformSerivce
    {
        private readonly IChannelRepository _channelRepository;

        public abstract string PlatformName { get; }
        public abstract int Interval { get; }

        private int _elapsedTime = 0;

        public PlatformService(
            IChannelRepository channelRepository)
        {
            _channelRepository = channelRepository;
        }

        List<Channel> IPlatformSerivce.GetMonitoringChannels()
            => _channelRepository.GetMonitoringChannels()
                                 .Where(p => p.Source == PlatformName)
                                 .ToList();

        public abstract Task UpdateVideosDataAsync(Channel channel, CancellationToken cancellation = default);

        public bool StepInterval(int elapsedTime)
        {
            _elapsedTime += elapsedTime;
            if (_elapsedTime >= Interval)
            {
                _elapsedTime = 0;
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
