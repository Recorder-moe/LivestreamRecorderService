﻿using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces.Job.Downloader;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices.ACI.Downloader;

public class StreamlinkService(
    ILogger<StreamlinkService> logger,
    ArmClient armClient,
    IOptions<AzureOption> options) : ACIServiceBase(logger, armClient, options), IStreamlinkService
{
    private readonly AzureOption _azureOption = options.Value;

    public override string Name => IStreamlinkService.name;

    public override Task InitJobAsync(string videoId,
                                      Video video,
                                      bool useCookiesFile = false,
                                      CancellationToken cancellation = default)
    {
        string filename = NameHelper.GetFileName(video, IStreamlinkService.name);
        video.Filename = filename;
        return InitJobWithChannelNameAsync(videoId: videoId,
                                           video: video,
                                           useCookiesFile: useCookiesFile,
                                           cancellation: cancellation);
    }

    protected override Task<ArmOperation<ArmDeploymentResource>> CreateNewJobAsync(
        string id,
        string instanceName,
        Video video,
        bool useCookiesFile = false,
        CancellationToken cancellation = default)
    {
        string filename = NameHelper.GetFileName(video, IStreamlinkService.name);
        try
        {
            return doWithImage("ghcr.io/recorder-moe/streamlink:latest");
        }
        // skipcq: CS-R1008
        catch (Exception)
        {
            // Use DockerHub as fallback
            logger.LogWarning("Failed once, try docker hub as fallback.");
            return doWithImage("recordermoe/streamlink:latest");
        }

        Task<ArmOperation<ArmDeploymentResource>> doWithImage(string imageName)
        {
            return CreateResourceAsync(
                    parameters: new
                    {
                        dockerImageName = new
                        {
                            value = imageName
                        },
                        containerName = new
                        {
                            value = instanceName
                        },
                        commandOverrideArray = new
                        {
                            value = new[] {
                                "/bin/sh", "-c",
                                $"streamlink --twitch-disable-ads -o '/download/{filename}' -f 'twitch.tv/{NameHelper.ChangeId.ChannelId.PlatformType(video.ChannelId, Name)}' best && cd /download && for file in *.mp4; do ffmpeg -i \"$file\" -map 0:v:0 -map 0:a:0 -c copy -movflags +faststart 'temp.mp4' && mv 'temp.mp4' \"/sharedvolume/$file\"; done"
                            }
                        },
                        storageAccountName = new
                        {
                            value = _azureOption.FileShare!.StorageAccountName
                        },
                        storageAccountKey = new
                        {
                            value = _azureOption.FileShare!.StorageAccountKey
                        },
                        fileshareVolumeName = new
                        {
                            value = _azureOption.FileShare!.ShareName
                        }
                    },
                    deploymentName: instanceName,
                    cancellation: cancellation);
        }
    }
}
