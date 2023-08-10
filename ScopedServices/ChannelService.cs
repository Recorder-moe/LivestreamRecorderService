#if COSMOSDB
using LivestreamRecorder.DB.CosmosDB;
using Microsoft.EntityFrameworkCore;
#elif COUCHDB
using CouchDB.Driver.Extensions;
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
        var channel = await _channelRepository.GetByChannelIdAndSource(video.ChannelId, video.Source);
        if (null == channel) return;

        channel!.LatestVideoId = video.id;
        await _channelRepository.AddOrUpdate(channel);
        _unitOfWork_Public.Commit();
    }

    public Task<Channel?> GetByChannelIdAndSource(string channelId, string source)
        => _channelRepository.GetByChannelIdAndSource(channelId, source);
}
