namespace LivestreamRecorderService.Interfaces.Job.Uploader;

public interface IS3UploaderService : IJobServiceBase
{
    public const string name = "s3uploader";
}