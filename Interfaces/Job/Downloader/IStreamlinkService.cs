namespace LivestreamRecorderService.Interfaces.Job.Downloader;

public interface IStreamlinkService : IJobServiceBase
{
    public const string name = "streamlink";
}