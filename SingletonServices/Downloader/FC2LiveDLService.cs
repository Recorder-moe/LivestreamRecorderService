using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces;

namespace LivestreamRecorderService.SingletonServices.Downloader;

public class Fc2LiveDLService(IJobService jobService) : IFc2LiveDLService
{
    private static string Name => IFc2LiveDLService.Name;

    public Task CreateJobAsync(Video video,
                               bool useCookiesFile = false,
                               string? url = null,
                               CancellationToken cancellation = default)
    {
        url ??= $"https://live.fc2.com/{NameHelper.ChangeId.ChannelId.PlatformType(video.ChannelId, Name)}";

        string fileName = NameHelper.GetFileName(video, Name);
        video.Filename = fileName;

        const string mountPath = "/recordings";
        string instanceName = NameHelper.GetInstanceName(Name, video.id);
        string[] args =
        [
            "--latency", "high",
            "--threads", "1",
            "-o", Path.ChangeExtension(fileName, ".%(ext)s"),
            "--log-level", "trace",
            url
        ];

        if (useCookiesFile) args = ["--cookies", $"/cookies/{video.ChannelId}.txt", .. args];

        return jobService.CreateInstanceAsync(deploymentName: instanceName,
                                              containerName: instanceName,
                                              imageName: "fc2-live-dl",
                                              fileName: fileName,
                                              args: args,
                                              mountPath: mountPath,
                                              cancellation: cancellation);
    }
}
