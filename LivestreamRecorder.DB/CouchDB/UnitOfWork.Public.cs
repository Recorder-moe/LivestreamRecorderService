#if COUCHDB
using LivestreamRecorder.DB.Models;

namespace LivestreamRecorder.DB.CouchDB
{
    public class UnitOfWork_Public : UnitOfWork
    {
        private readonly CouchDBContext _context;

        public UnitOfWork_Public(CouchDBContext context)
            : base(context)
        {
            _context = context;
            PrepareIndexsAsync().ConfigureAwait(false).GetAwaiter();
        }

        protected async Task PrepareIndexsAsync()
        {
            var tasks = new List<Task>();
            await prepareVideoIndex(tasks);
            await prepareChannelIndex(tasks);
            await Task.WhenAll(tasks);

            async Task prepareVideoIndex(List<Task> tasks)
            {
                var database = _context.Client.GetDatabase<Video>();
                var existIndexs = await database.GetIndexesAsync();
                foreach (var index in _context._videoIndexs)
                {
                    if (existIndexs.All(p => p.Name != index.Key))
                    {
                        tasks.Add(database.CreateIndexAsync(index.Key, index.Value, new() { Partitioned = false, }));
                    }
                }
            }

            async Task prepareChannelIndex(List<Task> tasks)
            {
                var database = _context.Client.GetDatabase<Channel>();
                var existIndexs = await database.GetIndexesAsync();
                foreach (var index in _context._channelIndexs)
                {
                    if (existIndexs.All(p => p.Name != index.Key))
                    {
                        tasks.Add(database.CreateIndexAsync(index.Key, index.Value, new() { Partitioned = false, }));
                    }

                }
            }
        }
    }
}
#endif
