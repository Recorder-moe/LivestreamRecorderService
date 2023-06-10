using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using LivestreamRecorderService.Interfaces.Job;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices.ACI;

public class StreamlinkService : ACIServiceBase, IStreamlinkService
{
    private readonly AzureOption _azureOption;
    private readonly ILogger<StreamlinkService> _logger;

    public override string DownloaderName => "streamlink";

    public StreamlinkService(
        ILogger<StreamlinkService> logger,
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
            return doWithImage("ghcr.io/recorder-moe/streamlink:5.3.1");
        }
        catch (Exception)
        {
            // Use DockerHub as fallback
            _logger.LogWarning("Failed once, try docker hub as fallback.");
            return doWithImage("recordermoe/streamlink:5.3.1");
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
                            value = _azureOption.AzureFileShare!.StorageAccountName
                        },
                        storageAccountKey = new
                        {
                            value = _azureOption.AzureFileShare!.StorageAccountKey
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
