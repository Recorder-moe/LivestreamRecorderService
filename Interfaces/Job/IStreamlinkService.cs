namespace LivestreamRecorderService.Interfaces.Job;

public interface IStreamlinkService : IJobServiceBase
{
    public const string downloaderName = "streamlink";
}