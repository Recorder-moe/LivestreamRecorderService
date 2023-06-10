using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using LivestreamRecorderService.Interfaces.Job;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices.ACI;

public class TwitcastingRecorderService : ACIServiceBase, ITwitcastingRecorderService
{
    private readonly AzureOption _azureOption;
    private readonly ILogger<TwitcastingRecorderService> _logger;

    public override string DownloaderName => "twitcastingrecorder";

    public TwitcastingRecorderService(
        ILogger<TwitcastingRecorderService> logger,
        ArmClient armClient,
        IOptions<AzureOption> options) : base(logger, armClient, options)
    {
        _azureOption = options.Value;
        _logger = logger;
    }

    protected override Task<ArmOperation<ArmDeploymentResource>> CreateNewInstance(string id,
                                                                                   string instanceName,
                                                                                   string channelId,
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
            return CreateAzureContainerInstanceAsync(
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
                                $"/bin/bash record_twitcast.sh {channelId} once && mv /download/*.mp4 /fileshare/"
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
