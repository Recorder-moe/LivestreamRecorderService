#if COSMOSDB
using LivestreamRecorder.DB.CosmosDB;
#elif COUCHDB
using LivestreamRecorder.DB.CouchDB;
#endif
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

    public async Task UpdateChannelLatestVideoAsync(Video video)
    {
        if (!_channelRepository.Exists(video.ChannelId)) return;

        var channel = await _channelRepository.GetById(video.ChannelId);
        channel!.LatestVideoId = video.id;
        await _channelRepository.AddOrUpdate(channel);
        _unitOfWork_Public.Commit();
    }

    public Task<Channel?> GetChannel(string channelId)
        => _channelRepository.GetById(channelId);
}
