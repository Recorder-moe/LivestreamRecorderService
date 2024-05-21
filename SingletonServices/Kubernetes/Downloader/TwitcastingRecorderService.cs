using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Interfaces.Job.Downloader;

namespace LivestreamRecorderService.SingletonServices.Kubernetes.Downloader;

public class TwitcastingRecorderService(
    ILogger<TwitcastingRecorderService> logger,
    k8s.Kubernetes kubernetes,
    IUploaderService uploaderService) : KubernetesServiceBase(logger,
                                                              kubernetes,
                                                              uploaderService), ITwitcastingRecorderService
{
    public override string Name => ITwitcastingRecorderService.Name;

    public override Task CreateJobAsync(Video video,
                                        bool useCookiesFile = false,
                                        string? url = null,
                                        CancellationToken cancellation = default)
    {
        string channelId = url?.Split('/', StringSplitOptions.RemoveEmptyEntries).Last()
                           ?? NameHelper.ChangeId.ChannelId.PlatformType(video.ChannelId, Name);

        string fileName = NameHelper.GetFileName(video, ITwitcastingRecorderService.Name);
        video.Filename = fileName;

        const string mountPath = "/download";
        string instanceName = GetInstanceName(video.id);
        string[] args =
        [
            channelId,
            "once",
            "-o", Path.GetFileNameWithoutExtension(fileName)
        ];

        return CreateInstanceAsync(deploymentName: instanceName,
                                   containerName: instanceName,
                                   imageName: "twitcasting-recorder",
                                   fileName: fileName,
                                   args: args,
                                   mountPath: mountPath,
                                   cancellation: cancellation);
    }
}
