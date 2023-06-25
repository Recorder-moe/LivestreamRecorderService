using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces.Job.Downloader;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices.ACI.Downloader;

public class YtarchiveService : ACIServiceBase, IYtarchiveService
{
    private readonly ILogger<YtarchiveService> _logger;
    private readonly AzureOption _azureOption;

    public override string Name => IYtarchiveService.name;

    public YtarchiveService(
        ILogger<YtarchiveService> logger,
        ArmClient armClient,
        IOptions<AzureOption> options) : base(logger, armClient, options)
    {
        _logger = logger;
        _azureOption = options.Value;
    }

    protected override Task<ArmOperation<ArmDeploymentResource>> CreateNewJobAsync(
        string url,
        string instanceName,
        Video video,
        bool useCookiesFile,
        CancellationToken cancellation)
    {
        try
        {
            return doWithImage("ghcr.io/recorder-moe/ytarchive:v0.3.2");
        }
        catch (Exception)
        {
            // Use DockerHub as fallback
            _logger.LogWarning("Failed once, try docker hub as fallback.");
            return doWithImage("recordermoe/ytarchive:v0.3.2");
        }

        Task<ArmOperation<ArmDeploymentResource>> doWithImage(string imageName)
        {
            string[] command = useCookiesFile
                ? new string[]
                {
                    "/usr/bin/dumb-init", "--",
                    "sh", "-c",
                    // It is possible for Youtube to use "-" at the beginning of an id, which can cause errors when using the id as a file name.
                    // Therefore, we add "_" before the file name to avoid such issues.
                    $"/ytarchive --add-metadata --merge --retry-frags 30 --thumbnail -o '_{NameHelper.GetFileName(video, IYtarchiveService.name).Replace(".mp4", "")}' -c /sharedvolume/cookies/{video.ChannelId}.txt '{url}' best && mv *.mp4 /sharedvolume/"
                }
                : new string[] {
                    "/usr/bin/dumb-init", "--",
                    "sh", "-c",
                    // It is possible for Youtube to use "-" at the beginning of an id, which can cause errors when using the id as a file name.
                    // Therefore, we add "_" before the file name to avoid such issues.
                    $"/ytarchive --add-metadata --merge --retry-frags 30 --thumbnail -o '_{NameHelper.GetFileName(video, IYtarchiveService.name).Replace(".mp4", "")}' '{url}' best && mv *.mp4 /sharedvolume/"
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
                            value = _azureOption.FileShare!.ShareName
                        }
                    },
                    deploymentName: instanceName,
                    cancellation: cancellation);
        }
    }
}
