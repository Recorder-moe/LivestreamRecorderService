using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Interfaces.Job.Downloader;

namespace LivestreamRecorderService.SingletonServices.Kubernetes.Downloader;

public class YtarchiveService(
    ILogger<YtarchiveService> logger,
    k8s.Kubernetes kubernetes,
    IUploaderService uploaderService) : KubernetesServiceBase(logger,
                                                              kubernetes,
                                                              uploaderService), IYtarchiveService
{
    public override string Name => IYtarchiveService.Name;

    public override Task CreateJobAsync(Video video,
                                        bool useCookiesFile = false,
                                        string? url = null,
                                        CancellationToken cancellation = default)
    {
        url ??= $"https://youtu.be/{NameHelper.ChangeId.VideoId.PlatformType(video.id, Name)}";

        string fileName = NameHelper.GetFileName(video, IYtarchiveService.Name);
        video.Filename = fileName;

        string instanceName = GetInstanceName(video.id);
        const string mountPath = "/download";
        string[] args =
        [
            "--add-metadata",
            "--merge",
            "--retry-frags", "30",
            "--thumbnail",
            "-o", fileName.Replace(".mp4", ""),
            url,
            "best"
        ];

        if (useCookiesFile) args = ["-c", $"/cookies/{video.ChannelId}.txt", .. args];

        return CreateInstanceAsync(deploymentName: instanceName,
                                   containerName: instanceName,
                                   imageName: "ytarchive",
                                   fileName: fileName,
                                   args: args,
                                   mountPath: mountPath,
                                   cancellation: cancellation);
    }
}
