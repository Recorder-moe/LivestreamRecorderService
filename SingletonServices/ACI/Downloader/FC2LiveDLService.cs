using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces.Job.Downloader;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices.ACI.Downloader;

public class FC2LiveDLService(
    ILogger<FC2LiveDLService> logger,
    ArmClient armClient,
    IOptions<AzureOption> options) : ACIServiceBase(logger, armClient, options), IFC2LiveDLService
{
    private readonly AzureOption _azureOption = options.Value;

    public override string Name => IFC2LiveDLService.name;

    public override Task InitJobAsync(string videoId,
                                      Video video,
                                      bool useCookiesFile = false,
                                      CancellationToken cancellation = default)
    {
        string filename = NameHelper.GetFileName(video, IFC2LiveDLService.name);
        video.Filename = filename;
        return InitJobWithChannelNameAsync(videoId: videoId,
                                           video: video,
                                           useCookiesFile: useCookiesFile,
                                           cancellation: cancellation);
    }

    protected override Task<ArmOperation<ArmDeploymentResource>> CreateNewJobAsync(
        string _,
        string instanceName,
        Video video,
        bool useCookiesFile = false,
        CancellationToken cancellation = default)
    {
        string filename = NameHelper.GetFileName(video, IFC2LiveDLService.name);
        try
        {
            return doWithImage("ghcr.io/recorder-moe/fc2-live-dl:latest");
        }
        catch (Exception)
        {
            // Use DockerHub as fallback
            logger.LogWarning("Failed once, try docker hub as fallback.");
            return doWithImage("recordermoe/fc2-live-dl:latest");
        }

        Task<ArmOperation<ArmDeploymentResource>> doWithImage(string imageName)
        {
            string[] command = useCookiesFile
                ?
                [
                    "dumb-init",
                    "--",
                    "sh",
                    "-c",
                    $"fc2-live-dl --latency high --threads 1 -o '{Path.ChangeExtension(filename, ".%(ext)s")}' --log-level trace --cookies /sharedvolume/cookies/{video.ChannelId}.txt 'https://live.fc2.com/{NameHelper.ChangeId.ChannelId.PlatformType(video.ChannelId, Name)}' && mv '/recordings/{filename}' /sharedvolume/"
                ]
                : [
                    "dumb-init",
                    "--",
                    "sh",
                    "-c",
                    $"fc2-live-dl --latency high --threads 1 -o '{Path.ChangeExtension(filename, ".%(ext)s")}' --log-level trace 'https://live.fc2.com/{NameHelper.ChangeId.ChannelId.PlatformType(video.ChannelId, Name)}' && mv '/recordings/{filename}' /sharedvolume/"
                ];

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
                            value = command
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
                            value = _azureOption.FileShare.ShareName
                        },
                        mountPath = new
                        {
                            value = "/sharedvolume"
                        },
                    },
                    deploymentName: instanceName,
                    cancellation: cancellation);
        }
    }
}
