namespace LivestreamRecorderService.Interfaces.Job.Uploader;

public interface IAzureUploaderService : IJobServiceBase
{
    public const string name = "azureuploader";
}