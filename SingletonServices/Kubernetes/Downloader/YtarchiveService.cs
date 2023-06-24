using k8s.Models;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces.Job.Downloader;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices.Kubernetes.Downloader;

public class YtarchiveService : KubernetesServiceBase, IYtarchiveService
{
    private readonly ILogger<YtarchiveService> _logger;

    public override string Name => IYtarchiveService.name;

    public YtarchiveService(
        ILogger<YtarchiveService> logger,
        k8s.Kubernetes kubernetes,
        IOptions<KubernetesOption> options,
        IOptions<ServiceOption> serviceOptions,
        IOptions<AzureOption> azureOptions,
        IOptions<NFSOption> nfsOptions) : base(logger, kubernetes, options, serviceOptions, azureOptions, nfsOptions)
    {
        _logger = logger;
    }

    public override async Task InitJobAsync(string videoId,
                                            Video video,
                                            bool useCookiesFile = false,
                                            CancellationToken cancellation = default)
    {
        if (null != await GetJobByKeywordAsync(videoId, cancellation))
        {
            _logger.LogWarning("K8s job already exists! Fixed {videoId} status mismatch.", videoId);
            return;
        }

        var url = $"https://youtu.be/{videoId}";
        await CreateNewJobAsync(url: url,
                                instanceName: GetInstanceName(url),
                                video: video,
                                useCookiesFile: useCookiesFile,
                                cancellation: cancellation);
    }

    protected override Task<V1Job> CreateNewJobAsync(string url,
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

        Task<V1Job> doWithImage(string imageName)
        {
            string[] command = useCookiesFile
                ? new string[]
                {
                    "/usr/bin/dumb-init", "--",
                    "sh", "-c",
                    // It is possible for Youtube to use "-" at the beginning of an id, which can cause errors when using the id as a file name.
                    // Therefore, we add "_" before the file name to avoid such issues.
                    $"/ytarchive --add-metadata --merge --retry-frags 30 --thumbnail -o '{NameHelper.GetFileName(video, IYtarchiveService.name).Replace(".mp4", "")}' -c /sharedvolume/cookies/{video.ChannelId}.txt '{url}' best && mv *.mp4 /sharedvolume/"
                }
                : new string[] {
                    "/usr/bin/dumb-init", "--",
                    "sh", "-c",
                    // It is possible for Youtube to use "-" at the beginning of an id, which can cause errors when using the id as a file name.
                    // Therefore, we add "_" before the file name to avoid such issues.
                    $"/ytarchive --add-metadata --merge --retry-frags 30 --thumbnail -o '{NameHelper.GetFileName(video, IYtarchiveService.name).Replace(".mp4", "")}' '{url}' best && mv *.mp4 /sharedvolume/"
                };

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
