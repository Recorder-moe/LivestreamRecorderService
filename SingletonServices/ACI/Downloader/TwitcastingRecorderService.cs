using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces.Job.Downloader;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices.ACI.Downloader;

public class TwitcastingRecorderService(
    ILogger<TwitcastingRecorderService> logger,
    ArmClient armClient,
    IOptions<AzureOption> options) : ACIServiceBase(logger, armClient, options), ITwitcastingRecorderService
{
    private readonly AzureOption _azureOption = options.Value;

    public override string Name => ITwitcastingRecorderService.name;

    public override Task InitJobAsync(string videoId,
                                      Video video,
                                      bool useCookiesFile = false,
                                      CancellationToken cancellation = default)
    {
        string filename = NameHelper.GetFileName(video, ITwitcastingRecorderService.name);
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
        try
        {
            return doWithImage("ghcr.io/recorder-moe/twitcasting-recorder:latest");
        }
        // skipcq: CS-R1008
        catch (Exception)
        {
            // Use DockerHub as fallback
            logger.LogWarning("Failed once, try docker hub as fallback.");
            return doWithImage("recordermoe/twitcasting-recorder:latest");
        }

        Task<ArmOperation<ArmDeploymentResource>> doWithImage(string imageName)
        {
            string filename = NameHelper.GetFileName(video, ITwitcastingRecorderService.name);
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
                                "dumb-init", "--",
                                "/bin/sh", "-c",
                                $"/bin/sh /app/record_twitcast.sh {NameHelper.ChangeId.ChannelId.PlatformType(video.ChannelId, Name)} once -o {Path.GetFileNameWithoutExtension(filename)} && mv /download/*.mp4 /sharedvolume/"
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
