using k8s.Models;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Interfaces.Job.Downloader;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices.Kubernetes.Downloader;

public class Fc2LiveDLService(
    ILogger<Fc2LiveDLService> logger,
    k8s.Kubernetes kubernetes,
    IUploaderService uploaderService) : KubernetesServiceBase(logger,
                                                              kubernetes,
                                                              uploaderService), IFc2LiveDLService
{
    public override string Name => IFc2LiveDLService.Name;

    protected override Task<V1Job> CreateNewJobAsync(string _,
                                                     string instanceName,
                                                     Video video,
                                                     bool useCookiesFile = false,
                                                     CancellationToken cancellation = default)
    {
        const string mountPath = "/recordings";
        string fileName = NameHelper.GetFileName(video, IFc2LiveDLService.Name);
        video.Filename = fileName;
        string[] args = useCookiesFile
            ?
            [
                "--latency", "high",
                "--threads", "1",
                "-o", Path.ChangeExtension(fileName, ".%(ext)s"),
                "--log-level", "trace",
                "--cookies", $"{mountPath}/cookies/{video.ChannelId}.txt",
                $"https://live.fc2.com/{NameHelper.ChangeId.ChannelId.PlatformType(video.ChannelId, Name)}"
            ]
            :
            [
                "--latency", "high",
                "--threads", "1",
                "-o", Path.ChangeExtension(fileName, ".%(ext)s"),
                "--log-level", "trace",
                $"https://live.fc2.com/{NameHelper.ChangeId.ChannelId.PlatformType(video.ChannelId, Name)}"
            ];

        try
        {
            return CreateInstanceAsync(deploymentName: instanceName,
                                       containerName: instanceName,
                                       imageName: "fc2-live-dl",
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
                                       imageName: "fc2-live-dl",
                                       args: args,
                                       fileName: fileName,
                                       mountPath: mountPath,
                                       fallback: true,
                                       cancellation: cancellation);
        }
    }
}
