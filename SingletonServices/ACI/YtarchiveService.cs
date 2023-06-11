using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using LivestreamRecorderService.Interfaces.Job;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices.ACI;

public class YtarchiveService : ACIServiceBase, IYtarchiveService
{
    private readonly ILogger<YtarchiveService> _logger;
    private readonly AzureOption _azureOption;

    public override string DownloaderName => "ytarchive";

    public YtarchiveService(
        ILogger<YtarchiveService> logger,
        ArmClient armClient,
        IOptions<AzureOption> options) : base(logger, armClient, options)
    {
        _logger = logger;
        _azureOption = options.Value;
    }

    public override async Task<dynamic> InitJobAsync(string videoId,
                                                           string channelId,
                                                           bool useCookiesFile = false,
                                                           CancellationToken cancellation = default)
    {
        if (null != await GetInstanceByKeywordAsync(videoId, cancellation))
        {
            _logger.LogWarning("ACI already exists! Fixed {videoId} status mismatch.", videoId);
            return Task.CompletedTask;
        }
        else
        {
            var url = $"https://youtu.be/{videoId}";
            return CreateNewJobAsync(id: url,
                                     instanceName: GetInstanceName(url),
                                     channelId: channelId,
                                     useCookiesFile: useCookiesFile,
                                     cancellation: cancellation);
        }
    }

    protected override Task<ArmOperation<ArmDeploymentResource>> CreateNewJobAsync(string id,
                                                                                   string instanceName,
                                                                                   string channelId,
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
                    $"/ytarchive --add-metadata --merge --retry-frags 30 --thumbnail -o '_%(id)s' -c /fileshare/cookies/{channelId}.txt '{id}' best && mv *.mp4 /fileshare/"
                }
                : new string[] {
                    "/usr/bin/dumb-init", "--",
                    "sh", "-c",
                    // It is possible for Youtube to use "-" at the beginning of an id, which can cause errors when using the id as a file name.
                    // Therefore, we add "_" before the file name to avoid such issues.
                    $"/ytarchive --add-metadata --merge --retry-frags 30 --thumbnail -o '_%(id)s' '{id}' best && mv *.mp4 /fileshare/"
                };

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
                            value = "livestream-recorder"
                        }
                    },
                    deploymentName: instanceName,
                    cancellation: cancellation);
        }
    }
}
