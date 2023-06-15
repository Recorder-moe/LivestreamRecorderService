using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces.Job;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices.ACI;

public class TwitcastingRecorderService : ACIServiceBase, ITwitcastingRecorderService
{
    private readonly AzureOption _azureOption;
    private readonly ILogger<TwitcastingRecorderService> _logger;

    public override string DownloaderName => ITwitcastingRecorderService.downloaderName;

    public TwitcastingRecorderService(
        ILogger<TwitcastingRecorderService> logger,
        ArmClient armClient,
        IOptions<AzureOption> options) : base(logger, armClient, options)
    {
        _azureOption = options.Value;
        _logger = logger;
    }

    protected override Task<ArmOperation<ArmDeploymentResource>> CreateNewJobAsync(string id,
                                                                                   string instanceName,
                                                                                   Video video,
                                                                                   bool useCookiesFile = false,
                                                                                   CancellationToken cancellation = default)
    {
        try
        {
            return doWithImage("ghcr.io/recorder-moe/twitcasting-recorder:latest");
        }
        catch (Exception)
        {
            // Use DockerHub as fallback
            _logger.LogWarning("Failed once, try docker hub as fallback.");
            return doWithImage("recordermoe/twitcasting-recorder:latest");
        }

        Task<ArmOperation<ArmDeploymentResource>> doWithImage(string imageName)
        {
            return CreateInstanceAsync(
                    template: "ACI.json",
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
                            value = new string[] {
                                "/usr/bin/dumb-init", "--",
                                "/bin/bash", "-c",
                                $"/bin/bash record_twitcast.sh {video.ChannelId} once && mv /download/{NameHelper.GetFileName(video, ITwitcastingRecorderService.downloaderName)} /fileshare/{NameHelper.GetFileName(video, ITwitcastingRecorderService.downloaderName)}"
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
                            value = "livestream-recorder"
                        }
                    },
                    deploymentName: instanceName,
                    cancellation: cancellation);
        }
    }
}
