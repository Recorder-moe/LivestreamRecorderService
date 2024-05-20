using k8s.Models;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Interfaces.Job.Downloader;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices.Kubernetes.Downloader;

public class TwitcastingRecorderService(
    ILogger<TwitcastingRecorderService> logger,
    k8s.Kubernetes kubernetes,
    IUploaderService uploaderService) : KubernetesServiceBase(logger,
                                                              kubernetes,
                                                              uploaderService), ITwitcastingRecorderService
{
    public override string Name => ITwitcastingRecorderService.Name;

    protected override Task<V1Job> CreateNewJobAsync(string _,
                                                     string instanceName,
                                                     Video video,
                                                     bool useCookiesFile = false,
                                                     CancellationToken cancellation = default)
    {
        const string mountPath = "/download";
        string fileName = NameHelper.GetFileName(video, ITwitcastingRecorderService.Name);
        video.Filename = fileName;
        string[] args =
        [
            NameHelper.ChangeId.ChannelId.PlatformType(video.ChannelId, Name),
            "once",
            "-o", Path.GetFileNameWithoutExtension(fileName)
        ];

        try
        {
            return CreateInstanceAsync(deploymentName: instanceName,
                                       containerName: instanceName,
                                       imageName: "twitcasting-recorder",
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
                                       imageName: "twitcasting-recorder",
                                       args: args,
                                       fileName: fileName,
                                       mountPath: mountPath,
                                       fallback: true,
                                       cancellation: cancellation);
        }
    }
}
