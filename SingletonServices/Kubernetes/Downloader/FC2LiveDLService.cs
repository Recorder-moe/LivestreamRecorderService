using k8s.Models;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces.Job.Downloader;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices.Kubernetes.Downloader;

public class FC2LiveDLService(
    ILogger<FC2LiveDLService> logger,
    k8s.Kubernetes kubernetes,
    IOptions<KubernetesOption> options,
    IOptions<ServiceOption> serviceOptions,
    IOptions<AzureOption> azureOptions) : KubernetesServiceBase(logger, kubernetes, options, serviceOptions, azureOptions), IFC2LiveDLService
{
    public override string Name => IFC2LiveDLService.name;

    protected override Task<V1Job> CreateNewJobAsync(string _,
                                                     string instanceName,
                                                     Video video,
                                                     bool useCookiesFile = false,
                                                     CancellationToken cancellation = default)
    {
        try
        {
            return doWithImage("ghcr.io/recorder-moe/fc2-live-dl:2.2.0");
        }
        catch (Exception)
        {
            // Use DockerHub as fallback
            logger.LogWarning("Failed once, try docker hub as fallback.");
            return doWithImage("recordermoe/fc2-live-dl:2.2.0");
        }

        Task<V1Job> doWithImage(string imageName)
        {
            string filename = NameHelper.GetFileName(video, IFC2LiveDLService.name);
            video.Filename = filename;
            string[] command = useCookiesFile
                ?
                [
                    "/usr/bin/dumb-init",
                    "--",
                    "sh",
                    "-c",
                    $"/venv/bin/fc2-live-dl --latency high --threads 1 -o '{Path.ChangeExtension(filename, ".%(ext)s")}' --log-level trace --cookies /sharedvolume/cookies/{video.ChannelId}.txt 'https://live.fc2.com/{video.ChannelId}/' && mv '/app/{filename}' /sharedvolume/"
                ]
                : [
                    "/usr/bin/dumb-init",
                    "--",
                    "sh",
                    "-c",
                    $"/venv/bin/fc2-live-dl --latency high --threads 1 -o '{Path.ChangeExtension(filename, ".%(ext)s")}' --log-level trace 'https://live.fc2.com/{video.ChannelId}/' && mv '/app/{filename}' /sharedvolume/"
                ];

            return CreateInstanceAsync(
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
                    },
                    deploymentName: instanceName,
                    cancellation: cancellation);
        }
    }
}
