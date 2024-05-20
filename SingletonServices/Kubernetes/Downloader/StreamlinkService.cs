using k8s.Models;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Interfaces.Job.Downloader;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices.Kubernetes.Downloader;

public class StreamlinkService(
    ILogger<StreamlinkService> logger,
    k8s.Kubernetes kubernetes,
    IUploaderService uploaderService) : KubernetesServiceBase(logger,
                                                              kubernetes,
                                                              uploaderService), IStreamlinkService
{
    public override string Name => IStreamlinkService.Name;

    protected override Task<V1Job> CreateNewJobAsync(string _,
                                                     string instanceName,
                                                     Video video,
                                                     bool useCookiesFile = false,
                                                     CancellationToken cancellation = default)
    {
        const string mountPath = "/download";
        string fileName = NameHelper.GetFileName(video, IStreamlinkService.Name);
        video.Filename = fileName;
        string[] command =
        [
            "/bin/sh", "-c",
        ];

        string[] args =
        [
            $"streamlink --twitch-disable-ads -o '{mountPath}/temp.mp4' -f 'twitch.tv/{NameHelper.ChangeId.ChannelId.PlatformType(video.ChannelId, Name)}' best && ffmpeg -i temp.mp4 -map 0:v:0 -map 0:a:0 -c copy -movflags +faststart '{fileName}' && rm temp.mp4"
        ];

        try
        {
            return CreateInstanceAsync(deploymentName: instanceName,
                                       containerName: instanceName,
                                       imageName: "streamlink",
                                       command: command,
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
                                       imageName: "streamlink",
                                       command: command,
                                       args: args,
                                       fileName: fileName,
                                       mountPath: mountPath,
                                       fallback: true,
                                       cancellation: cancellation);
        }
    }
}
