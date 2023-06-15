namespace LivestreamRecorderService.Interfaces.Job;

public interface IYtarchiveService : IJobServiceBase
{
    public const string downloaderName = "ytarchiveme";
}