#if COSMOSDB
using LivestreamRecorder.DB.CosmosDB;
using Microsoft.EntityFrameworkCore;
#elif COUCHDB
using LivestreamRecorder.DB.CouchDB;
#endif
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;

namespace LivestreamRecorderService.ScopedServices;

public class ChannelService(
    UnitOfWork_Public unitOfWork_Public,
    IChannelRepository channelRepository)
{
#pragma warning disable CA1859 // 盡可能使用具象類型以提高效能
    private readonly IUnitOfWork _unitOfWork_Public = unitOfWork_Public;

    public async Task UpdateChannelLatestVideoAsync(Video video)
    {
        var channel = await channelRepository.GetChannelByIdAndSourceAsync(video.ChannelId, video.Source);
        if (null == channel) return;

        channel!.LatestVideoId = video.id;
        await channelRepository.AddOrUpdateAsync(channel);
        _unitOfWork_Public.Commit();
    }

    public Task<Channel?> GetByChannelIdAndSource(string channelId, string source)
        => channelRepository.GetChannelByIdAndSourceAsync(channelId, source);
}
