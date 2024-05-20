using k8s.Models;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Interfaces.Job.Downloader;

namespace LivestreamRecorderService.SingletonServices.Kubernetes.Downloader;

public class YtdlpService(
    ILogger<YtdlpService> logger,
    k8s.Kubernetes kubernetes,
    IUploaderService uploaderService) : KubernetesServiceBase(logger,
                                                              kubernetes,
                                                              uploaderService), IYtdlpService
{
    public override string Name => IYtdlpService.Name;

    protected override Task<V1Job> CreateNewJobAsync(string url,
                                                     string instanceName,
                                                     Video video,
                                                     bool useCookiesFile = false,
                                                     CancellationToken cancellation = default)
    {
        if (!url.StartsWith("http")) url = $"https://youtu.be/{NameHelper.ChangeId.VideoId.PlatformType(url, Name)}";

        const string mountPath = "/download";
        string fileName = NameHelper.GetFileName(video, IYtdlpService.Name);
        video.Filename = fileName;
        string[] args = useCookiesFile
            ?
            [
                "--ignore-config",
                "--retries", "30",
                "--concurrent-fragments", "16",
                "--merge-output-format", "mp4",
                "-S", "+proto:http,+codec:h264",
                "--embed-thumbnail",
                "--embed-metadata",
                "--no-part",
                "--cookies", $"{mountPath}/cookies/{video.ChannelId}.txt",
                "-o", fileName,
                url,
            ]
            :
            [
                "--ignore-config",
                "--retries", "30",
                "--concurrent-fragments", "16",
                "--merge-output-format", "mp4",
                "-S", "+proto:http,+codec:h264",
                "--embed-thumbnail",
                "--embed-metadata",
                "--no-part",
                "-o", fileName,
                url,
            ];

        // Workaround for twitcasting ERROR:
        // Initialization fragment found after media fragments, unable to download
        // https://github.com/yt-dlp/yt-dlp/issues/5497
        if (url.Contains("twitcasting.tv"))
        {
            args = ["--downloader", "ffmpeg", .. args];
        }

        try
        {
            return CreateInstanceAsync(deploymentName: instanceName,
                                       containerName: instanceName,
                                       imageName: "yt-dlp",
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
                                       imageName: "yt-dlp",
                                       args: args,
                                       fileName: fileName,
                                       mountPath: mountPath,
                                       fallback: true,
                                       cancellation: cancellation);
        }
    }
}
