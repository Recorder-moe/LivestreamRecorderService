using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces.Job.Downloader;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices.ACI.Downloader;

public class FC2LiveDLService : ACIServiceBase, IFC2LiveDLService
{
    private readonly AzureOption _azureOption;
    private readonly ILogger<FC2LiveDLService> _logger;

    public override string Name => IFC2LiveDLService.name;

    public FC2LiveDLService(
        ILogger<FC2LiveDLService> logger,
        ArmClient armClient,
        IOptions<AzureOption> options) : base(logger, armClient, options)
    {
        _azureOption = options.Value;
        _logger = logger;
    }

    public override Task InitJobAsync(string videoId,
                                      Video video,
                                      bool useCookiesFile = false,
                                      CancellationToken cancellation = default)
    {
        string filename = NameHelper.GetFileName(video, IFC2LiveDLService.name);
        video.Filename = filename;
        return InitJobAsyncWithChannelName(videoId: videoId,
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
            return doWithImage("ghcr.io/recorder-moe/fc2-live-dl:2.1.3");
        }
        catch (Exception)
        {
            // Use DockerHub as fallback
            _logger.LogWarning("Failed once, try docker hub as fallback.");
            return doWithImage("recordermoe/fc2-live-dl:2.1.3");
        }

        Task<ArmOperation<ArmDeploymentResource>> doWithImage(string imageName)
        {
            string[] command = useCookiesFile
                ? new string[]
                {
                    "/usr/bin/dumb-init", "--",
                    "sh", "-c",
                    $"/venv/bin/fc2-live-dl --latency high --threads 1 -o '{filename}' --log-level trace --cookies /sharedvolume/cookies/{video.ChannelId}.txt 'https://live.fc2.com/{video.ChannelId}/' && mv *.mp4 /sharedvolume/"
                }
                : new string[] {
                    "/usr/bin/dumb-init", "--",
                    "sh", "-c",
                    $"/venv/bin/fc2-live-dl --latency high --threads 1 -o '{filename}' --log-level trace 'https://live.fc2.com/{video.ChannelId}/' && mv *.mp4 /sharedvolume/"
                };

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
                        }
                    },
                    deploymentName: instanceName,
                    cancellation: cancellation);
        }
    }
}
