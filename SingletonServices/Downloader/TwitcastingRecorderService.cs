using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces;

namespace LivestreamRecorderService.SingletonServices.Downloader;

public class TwitcastingRecorderService(IJobService jobService) : ITwitcastingRecorderService
{
    private static string Name => ITwitcastingRecorderService.Name;

    public Task CreateJobAsync(Video video,
                               bool useCookiesFile = false,
                               string? url = null,
                               CancellationToken cancellation = default)
    {
        string channelId = url?.Split('/', StringSplitOptions.RemoveEmptyEntries).Last()
                           ?? NameHelper.ChangeId.ChannelId.PlatformType(video.ChannelId, Name);

        string fileName = NameHelper.GetFileName(video, Name);
        video.Filename = fileName;

        const string mountPath = "/download";
        string instanceName = NameHelper.GetInstanceName(Name, video.id);
        string[] args =
        [
            channelId,
            "once",
            "-o", Path.GetFileNameWithoutExtension(fileName)
        ];

        return jobService.CreateInstanceAsync(deploymentName: instanceName,
                                              containerName: instanceName,
                                              imageName: "twitcasting-recorder",
                                              fileName: fileName,
                                              args: args,
                                              mountPath: mountPath,
                                              cancellation: cancellation);
    }
}
