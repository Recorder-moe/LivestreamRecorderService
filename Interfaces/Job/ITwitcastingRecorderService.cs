namespace LivestreamRecorderService.Interfaces.Job;

public interface ITwitcastingRecorderService : IJobServiceBase
{
    public const string downloaderName = "twitcastingrecorder";
}