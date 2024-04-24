namespace LivestreamRecorderService.Interfaces.Job.Uploader;

public interface IS3UploaderService : IJobServiceBase
{
    public new const string Name = "s3uploader";
}
