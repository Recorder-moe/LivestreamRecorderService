using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces;

namespace LivestreamRecorderService.SingletonServices.Downloader;

public class YtarchiveService(IJobService jobService) : IYtarchiveService
{
    private static string Name => IYtarchiveService.Name;

    public Task CreateJobAsync(Video video,
                               bool useCookiesFile = false,
                               string? url = null,
                               CancellationToken cancellation = default)
    {
        url ??= $"https://youtu.be/{NameHelper.ChangeId.VideoId.PlatformType(video.id, Name)}";

        string fileName = NameHelper.GetFileName(video, Name);
        video.Filename = fileName;

        string instanceName = NameHelper.GetInstanceName(Name, video.id);
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

        return jobService.CreateInstanceAsync(deploymentName: instanceName,
                                              containerName: instanceName,
                                              imageName: "ytarchive",
                                              fileName: fileName,
                                              args: args,
                                              mountPath: mountPath,
                                              cancellation: cancellation);
    }
}
