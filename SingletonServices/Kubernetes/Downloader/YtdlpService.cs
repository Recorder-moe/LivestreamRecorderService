﻿using LivestreamRecorder.DB.Models;
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

    public override Task CreateJobAsync(Video video,
                                        bool useCookiesFile = false,
                                        string? url = null,
                                        CancellationToken cancellation = default)
    {
        string instanceName = GetInstanceName(video.id);
        url ??= NameHelper.ChangeId.VideoId.PlatformType(video.id, Name);

        if (!url.StartsWith("http"))
            url = $"https://youtu.be/{NameHelper.ChangeId.VideoId.PlatformType(url, Name)}";

        const string mountPath = "/download";
        string fileName = NameHelper.GetFileName(video, IYtdlpService.Name);
        video.Filename = fileName;
        string[]? command = null;
        string[] args =
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
            url
        ];

        // Workaround for twitcasting ERROR:
        // Initialization fragment found after media fragments, unable to download
        // https://github.com/yt-dlp/yt-dlp/issues/5497
        if (url.Contains("twitcasting.tv")) args = ["--downloader", "ffmpeg", .. args];

        if (useCookiesFile)
            //args = ["--cookies", $"/cookies/{video.ChannelId}.txt", .. args];
            // Remove this workaround if issue resolved.
            // https://github.com/yt-dlp/yt-dlp/issues/5977#issuecomment-2121742572
            (command, args) = copyCookiesHack();

        return CreateInstanceAsync(deploymentName: instanceName,
                                   containerName: instanceName,
                                   imageName: "yt-dlp",
                                   fileName: fileName,
                                   command: command,
                                   args: args,
                                   mountPath: mountPath,
                                   cancellation: cancellation);

        (string[] command, string[] args) copyCookiesHack()
        {
            // Cookies file has to be mounted elsewhere and then cp.
            // Because yt-dlp does not support using cookies file in Read-only file system.
            // Which it is how K8s handle secrets.

            // original ENTRYPOINT: "dumb-init", "--", "yt-dlp", "--no-cache-dir"

            command = ["dumb-init", "--", "sh", "-c"];

            // cp under mountPath to make sure the permission is writable
            args =
            [
                $"cp -r /cookies {mountPath}/cookies && yt-dlp --ignore-config --retries 30 --concurrent-fragments 16 --merge-output-format mp4 -S '+proto:http,+codec:h264' --embed-thumbnail --embed-metadata --no-part --cookies {mountPath}/cookies/{video.ChannelId}.txt -o '{fileName}' '{url}'"
            ];

            // Workaround for twitcasting ERROR:
            // Initialization fragment found after media fragments, unable to download
            // https://github.com/yt-dlp/yt-dlp/issues/5497
            if (url.Contains("twitcasting.tv")) args[0] = args[0].Replace("--ignore-config", "--ignore-config --downloader ffmpeg");

            return (command, args);
        }
    }
}
