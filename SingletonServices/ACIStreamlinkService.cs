using Azure.ResourceManager;
using Azure.ResourceManager.ContainerInstance;
using Azure.ResourceManager.Resources;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices;

public class ACIStreamlinkService : ACIService
{
    private readonly AzureOption _azureOption;
    private readonly ILogger<ACIStreamlinkService> _logger;

    public override string DownloaderName => "streamlink";

    public ACIStreamlinkService(
        ILogger<ACIStreamlinkService> logger,
        ArmClient armClient,
        IOptions<AzureOption> options) : base(logger, armClient, options)
    {
        _azureOption = options.Value;
        _logger = logger;
    }

    protected override Task<ArmOperation<ArmDeploymentResource>> CreateNewInstance(string channelId, string instanceName, CancellationToken cancellation)
    {
        try
        {
            return doWithImage("ghcr.io/recorder-moe/streamlink:5.1.2");
        }
        catch (Exception)
        {
            // Use DockerHub as fallback
            _logger.LogWarning("Failed once, try docker hub as fallback.");
            return doWithImage("recorder-moe/streamlink:5.1.2");
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
                                "/bin/sh", "-c",
                                $"/usr/local/bin/streamlink --twitch-disable-ads -o '/downloads/{{id}}.mp4' -f 'twitch.tv/{channelId}' best && cd /downloads && for file in *.mp4; do ffmpeg -i \"$file\" -map 0:v:0 -map 0:a:0 -c copy -movflags +faststart 'temp.mp4' && mv 'temp.mp4' \"/fileshare/$file\"; done"
                            }
                        },
                        storageAccountName = new
                        {
                            value = _azureOption.StorageAccountName
                        },
                        storageAccountKey = new
                        {
                            value = _azureOption.StorageAccountKey
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
