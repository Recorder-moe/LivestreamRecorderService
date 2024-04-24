namespace LivestreamRecorderService.Interfaces.Job.Downloader;

public interface IStreamlinkService : IJobServiceBase
{
    public new const string Name = "streamlink";
}
