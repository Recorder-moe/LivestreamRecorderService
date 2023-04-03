using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.SingletonServices;

namespace LivestreamRecorderService.ScopedServices;

public class ChannelService
{
    private readonly ILogger<ChannelService> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IChannelRepository _channelRepository;
    private readonly DiscordService _discordService;

    public ChannelService(
        ILogger<ChannelService> logger,
        IUnitOfWork unitOfWork,
        IChannelRepository channelRepository,
        DiscordService discordService)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _channelRepository = channelRepository;
        _discordService = discordService;
    }

    public void UpdateChannelLatestVideo(Video video)
    {
        var channel = _channelRepository.GetById(video.ChannelId);
        _unitOfWork.Context.Entry(channel).Reload();
        channel.LatestVideoId = video.id;
        _channelRepository.Update(channel);
        _unitOfWork.Commit();
    }

    public async Task ConsumeSupportTokenAsync(Video video)
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

        if (channel.SupportToken == 0)
        {
            await _discordService.SendChannelSupportTokenZeroMessage(channel);
        }
        else if (channel.SupportToken <= 10)
        {
            await _discordService.SendChannelSupportTokenAlertMessage(channel);
        }
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
