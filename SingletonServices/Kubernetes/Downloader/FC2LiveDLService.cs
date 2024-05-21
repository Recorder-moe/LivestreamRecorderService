using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Interfaces.Job.Downloader;

namespace LivestreamRecorderService.SingletonServices.Kubernetes.Downloader;

public class Fc2LiveDLService(
    ILogger<Fc2LiveDLService> logger,
    k8s.Kubernetes kubernetes,
    IUploaderService uploaderService) : KubernetesServiceBase(logger,
                                                              kubernetes,
                                                              uploaderService), IFc2LiveDLService
{
    public override string Name => IFc2LiveDLService.Name;

    public override Task CreateJobAsync(Video video,
                                        bool useCookiesFile = false,
                                        string? url = null,
                                        CancellationToken cancellation = default)
    {
        url ??= $"https://live.fc2.com/{NameHelper.ChangeId.ChannelId.PlatformType(video.ChannelId, Name)}";

        string fileName = NameHelper.GetFileName(video, IFc2LiveDLService.Name);
        video.Filename = fileName;

        const string mountPath = "/recordings";
        string instanceName = GetInstanceName(video.id);
        string[] args =
        [
            "--latency", "high",
            "--threads", "1",
            "-o", Path.ChangeExtension(fileName, ".%(ext)s"),
            "--log-level", "trace",
            url
        ];

        if (useCookiesFile) args = ["--cookies", $"/cookies/{video.ChannelId}.txt", .. args];

        return CreateInstanceAsync(deploymentName: instanceName,
                                   containerName: instanceName,
                                   imageName: "fc2-live-dl",
                                   fileName: fileName,
                                   args: args,
                                   mountPath: mountPath,
                                   cancellation: cancellation);
    }
}
