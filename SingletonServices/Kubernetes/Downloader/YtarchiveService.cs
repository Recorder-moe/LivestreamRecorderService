using k8s.Models;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Interfaces.Job.Downloader;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices.Kubernetes.Downloader;

public class YtarchiveService(
    ILogger<YtarchiveService> logger,
    k8s.Kubernetes kubernetes,
    IUploaderService uploaderService) : KubernetesServiceBase(logger,
                                                              kubernetes,
                                                              uploaderService), IYtarchiveService
{
    public override string Name => IYtarchiveService.Name;

    protected override Task<V1Job> CreateNewJobAsync(string url,
                                                     string instanceName,
                                                     Video video,
                                                     bool useCookiesFile = false,
                                                     CancellationToken cancellation = default)
    {
        if (!url.StartsWith("http")) url = $"https://youtu.be/{NameHelper.ChangeId.VideoId.PlatformType(url, Name)}";

        const string mountPath = "/download";
        string fileName = NameHelper.GetFileName(video, IYtarchiveService.Name);
        video.Filename = fileName;
        string[] args = useCookiesFile
            ?
            [
                "--add-metadata",
                "--merge",
                "--retry-frags", "30",
                "--thumbnail",
                "-o", fileName.Replace(".mp4", ""),
                "-c", $"{mountPath}/cookies/{video.ChannelId}.txt",
                url,
                "best"
            ]
            :
            [
                "--add-metadata",
                "--merge",
                "--retry-frags", "30",
                "--thumbnail",
                "-o", fileName.Replace(".mp4", ""),
                url,
                "best"
            ];

        try
        {
            return CreateInstanceAsync(deploymentName: instanceName,
                                       containerName: instanceName,
                                       imageName: "ytarchive",
                                       args: args,
                                       fileName: fileName,
                                       mountPath: mountPath,
                                       fallback: false,
                                       cancellation: cancellation);
        }
        // skipcq: CS-R1008
        catch (Exception)
        {
            // Use DockerHub as fallback
            logger.LogWarning("Failed once, try docker hub as fallback.");
            return CreateInstanceAsync(deploymentName: instanceName,
                                       containerName: instanceName,
                                       imageName: "ytarchive",
                                       args: args,
                                       fileName: fileName,
                                       mountPath: mountPath,
                                       fallback: true,
                                       cancellation: cancellation);
        }
    }
}
