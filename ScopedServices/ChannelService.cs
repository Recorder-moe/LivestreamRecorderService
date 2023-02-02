using LivestreamRecorderService.DB.Interfaces;
using LivestreamRecorderService.DB.Models;

namespace LivestreamRecorderService.ScopedServices;

public class ChannelService
{
    private readonly ILogger<ChannelService> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IChannelRepository _channelRepository;

    public ChannelService(
        ILogger<ChannelService> logger,
        IUnitOfWork unitOfWork,
        IChannelRepository channelRepository)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _channelRepository = channelRepository;
    }

    public void UpdateChannelLatestVideo(Video video)
    {
        var channel = _channelRepository.GetById(video.ChannelId);
        _unitOfWork.Context.Entry(channel).Reload();
        channel.LatestVideoId = video.id;
        _channelRepository.Update(channel);
        _unitOfWork.Commit();
    }

    public void ConsumeSupportToken(Video video)
    {
        var channel = _channelRepository.GetById(video.ChannelId);
        _unitOfWork.Context.Entry(channel).Reload();

        decimal amount = CalculateConsumeSupportToken(video.Size);
        channel.SupportToken -= amount;

        if (channel.SupportToken < 0) channel.SupportToken = 0;

        if (channel.SupportToken == 0) channel.Monitoring = false;

        _channelRepository.Update(channel);
        _unitOfWork.Commit();
        _logger.LogDebug("Consume Channel {channelId} {amount} SupportToken ", channel.id, amount);
    }

    private static decimal CalculateConsumeSupportToken(long? size)
    {
        if (null == size) return 0m;

        decimal gb = (decimal)(size! / 1024.0m / 1024.0m / 1024.0m);
        return gb switch
        {
            < 0.5m => 0m,
            <= 1m => 1m,
            _ => Math.Floor(gb)
        };
    }
}
