using LivestreamRecorder.DB.Core;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;

namespace LivestreamRecorderService.ScopedServices;

public class ChannelService
{
    private readonly IUnitOfWork _unitOfWork_Public;
    private readonly IChannelRepository _channelRepository;

    public ChannelService(
        UnitOfWork_Public unitOfWork_Public,
        IChannelRepository channelRepository)
    {
        _unitOfWork_Public = unitOfWork_Public;
        _channelRepository = channelRepository;
    }

    public void UpdateChannelLatestVideo(Video video)
    {
        if (!_channelRepository.Exists(video.ChannelId)) return;

        var channel = _channelRepository.GetById(video.ChannelId);
        _unitOfWork_Public.Context.Entry(channel).Reload();
        channel.LatestVideoId = video.id;
        _channelRepository.Update(channel);
        _unitOfWork_Public.Commit();
    }
}
