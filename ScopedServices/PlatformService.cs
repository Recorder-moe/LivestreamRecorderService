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

        private static readonly Dictionary<string, int> _elapsedTime = new();

        public PlatformService(
            IChannelRepository channelRepository)
        {
            _channelRepository = channelRepository;
            if (!_elapsedTime.ContainsKey(PlatformName))
            {
                _elapsedTime.Add(PlatformName, 0);
            }
        }

        List<Channel> IPlatformSerivce.GetMonitoringChannels()
            => _channelRepository.GetMonitoringChannels()
                                 .Where(p => p.Source == PlatformName)
                                 .ToList();

        public abstract Task UpdateVideosDataAsync(Channel channel, CancellationToken cancellation = default);

        public bool StepInterval(int elapsedTime)
        {
            if (_elapsedTime[PlatformName] == 0)
            {
                _elapsedTime[PlatformName] += elapsedTime;
                return true;
            }

            _elapsedTime[PlatformName] += elapsedTime;
            if (_elapsedTime[PlatformName] >= Interval)
            {
                _elapsedTime[PlatformName] = 0;
            }
            return false;
        }
    }
}
