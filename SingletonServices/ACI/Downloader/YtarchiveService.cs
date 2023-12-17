using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces.Job.Downloader;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices.ACI.Downloader;

public class YtarchiveService(
    ILogger<YtarchiveService> logger,
    ArmClient armClient,
    IOptions<AzureOption> options) : ACIServiceBase(logger, armClient, options), IYtarchiveService
{
    private readonly AzureOption _azureOption = options.Value;

    public override string Name => IYtarchiveService.name;

    protected override Task<ArmOperation<ArmDeploymentResource>> CreateNewJobAsync(
        string url,
        string instanceName,
        Video video,
        bool useCookiesFile,
        CancellationToken cancellation)
    {
        if (!url.StartsWith("http")) url = $"https://youtu.be/{url}";

        try
        {
            return doWithImage("ghcr.io/recorder-moe/ytarchive:latest");
        }
        catch (Exception)
        {
            // Use DockerHub as fallback
            logger.LogWarning("Failed once, try docker hub as fallback.");
            return doWithImage("recordermoe/ytarchive:latest");
        }

        Task<ArmOperation<ArmDeploymentResource>> doWithImage(string imageName)
        {
            string filename = NameHelper.GetFileName(video, IYtarchiveService.name);
            video.Filename = filename;
            string[] command = useCookiesFile
                ?
                [
                    "sh",
                    "-c",
                    $"/usr/local/bin/ytarchive --add-metadata --merge --retry-frags 30 --thumbnail -o '{filename.Replace(".mp4", "")}' -c /sharedvolume/cookies/{video.ChannelId}.txt '{url}' best && mv *.mp4 /sharedvolume/"
                ]
                : [
                    "sh",
                    "-c",
                    $"/usr/local/bin/ytarchive --add-metadata --merge --retry-frags 30 --thumbnail -o '{filename.Replace(".mp4", "")}' '{url}' best && mv *.mp4 /sharedvolume/"
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
                            value = _azureOption.FileShare!.ShareName
                        }
                    },
                    deploymentName: instanceName,
                    cancellation: cancellation);
        }
    }
}
