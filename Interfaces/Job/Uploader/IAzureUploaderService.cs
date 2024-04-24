namespace LivestreamRecorderService.Interfaces.Job.Uploader;

public interface IAzureUploaderService : IJobServiceBase
{
    public new const string Name = "azureuploader";
}
