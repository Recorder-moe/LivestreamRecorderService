#if COSMOSDB
using LivestreamRecorder.DB.CosmosDB;
#elif COUCHDB
using LivestreamRecorder.DB.CouchDB;
#endif
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;

namespace LivestreamRecorderService.ScopedServices;

public class ChannelService(
    // ReSharper disable once SuggestBaseTypeForParameterInConstructor
    UnitOfWork_Public unitOfWorkPublic,
    IChannelRepository channelRepository)
{
#pragma warning disable CA1859
    private readonly IUnitOfWork _unitOfWorkPublic = unitOfWorkPublic;
#pragma warning restore CA1859

    public async Task UpdateChannelLatestVideoAsync(Video video)
    {
        Channel? channel = await channelRepository.GetChannelByIdAndSourceAsync(video.ChannelId, video.Source);
        if (null == channel) return;

        channel.LatestVideoId = video.id;
        await channelRepository.AddOrUpdateAsync(channel);
        _unitOfWorkPublic.Commit();
    }

    public Task<Channel?> GetByChannelIdAndSourceAsync(string channelId, string source)
        => channelRepository.GetChannelByIdAndSourceAsync(channelId, source);
}
