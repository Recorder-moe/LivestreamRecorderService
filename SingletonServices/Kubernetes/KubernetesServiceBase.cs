using k8s;
using k8s.Models;
using LivestreamRecorder.DB.Enum;
using LivestreamRecorderService.Interfaces.Job;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;
using System.Configuration;

namespace LivestreamRecorderService.SingletonServices.Kubernetes;

public abstract class KubernetesServiceBase : IJobServiceBase
{
    private readonly ILogger<KubernetesServiceBase> _logger;
    private readonly k8s.Kubernetes _client;
    private readonly KubernetesOption _option;
    private readonly ServiceOption _serviceOption;
    private readonly AzureOption _azureOption;
    private readonly NFSOption _nfsOption;

    public abstract string DownloaderName { get; }
    protected static string KubernetesNamespace => KubernetesService.KubernetesNamespace;

    public KubernetesServiceBase(
        ILogger<KubernetesServiceBase> logger,
        k8s.Kubernetes kubernetes,
        IOptions<KubernetesOption> options,
        IOptions<ServiceOption> serviceOptions,
        IOptions<AzureOption> azureOptions,
        IOptions<NFSOption> nfsOptions)
    {
        _logger = logger;
        _client = kubernetes;
        _option = options.Value;
        _serviceOption = serviceOptions.Value;
        _azureOption = azureOptions.Value;
        _nfsOption = nfsOptions.Value;
    }

    public virtual async Task<dynamic> InitJobAsync(string videoId, string channelId, bool useCookiesFile = false, CancellationToken cancellation = default)
    {
        // Warning: Unlike ACI, Kubernetes jobs cannot be rerun.
        var jobName = GetInstanceName(videoId);

        var job = await GetJobByKeywordAsync(jobName, cancellation);
        if (null != job && job.Status.Active != 0)
        {
            _logger.LogWarning("An already active job not found for {videoId} {name}!! A new job will now be created with different name. Please pay attention if this happens repeatedly.", videoId, jobName);
            jobName += DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        _logger.LogInformation("Start new K8s job for {videoId} {name}.", videoId, jobName);
        return await CreateNewJobAsync(id: videoId,
                                       instanceName: jobName,
                                       channelId: channelId,
                                       useCookiesFile: useCookiesFile,
                                       cancellation: cancellation);
    }

    protected async Task<V1Job?> GetJobByKeywordAsync(string keyword, CancellationToken cancellation)
    {
        var jobs = await _client.ListNamespacedJobAsync(KubernetesNamespace, cancellationToken: cancellation);
        return jobs.Items.FirstOrDefault(p => p.Name().Contains(GetInstanceName(keyword)));
    }

    protected Task<V1Job> CreateInstanceAsync(dynamic parameters, string deploymentName, CancellationToken cancellation = default)
    {
        V1Job job = new()
        {
            Metadata = new V1ObjectMeta
            {
                Name = deploymentName
            },
            Spec = new V1JobSpec
            {
                Completions = 1,
                Template = new V1PodTemplateSpec
                {
                    Spec = new V1PodSpec
                    {
                        RestartPolicy = "Never",
                        Containers = new List<V1Container>
                        {
                            new V1Container
                            {
                                Name = parameters.containerName.value,
                                Image = parameters.dockerImageName.value,
                                Command = parameters.commandOverrideArray.value,
                                VolumeMounts = new List<V1VolumeMount>
                                {
                                    new()
                                    {
                                        Name = "sharedvolume",
                                        MountPath = "/sharedvolume",
                                    }
                                },
                            }
                        },
                        Volumes = new List<V1Volume>
                        {
                            GetSharedVolumeDefinition()
                        }
                    }
                }
            }
        };

        return _client.CreateNamespacedJobAsync(body: job,
                                                namespaceParameter: KubernetesNamespace,
                                                cancellationToken: cancellation);
    }

    private V1Volume GetSharedVolumeDefinition()
        => _serviceOption.SharedVolumeService switch
        {
            ServiceName.AzureFileShare => new()
            {
                Name = "sharedvolume",
                AzureFile = new()
                {
                    SecretName = KubernetesService._azureFileShareSecretName,
                    ShareName = _azureOption.FileShare!.ShareName,
                    ReadOnlyProperty = false,
                }
            },
            ServiceName.NFS => new()
            {
                Name = "sharedvolume",
                Nfs = new()
                {
                    Server = _nfsOption.Server,
                    Path = _nfsOption.Path,
                    ReadOnlyProperty = false,
                },
                Secret = new ()
                {
                    SecretName = KubernetesService._nfsSecretName,
                }
            },
            _ => throw new ConfigurationErrorsException($"ShareVolume Service {Enum.GetName(typeof(ServiceName), _serviceOption.SharedVolumeService)} not supported for Kubernetes.")
        };

    protected string GetInstanceName(string videoId)
        => (DownloaderName + KubernetesService.GetInstanceName(videoId)).ToLower();

    // Must be override
    protected abstract Task<V1Job> CreateNewJobAsync(string id,
                                                     string instanceName,
                                                     string channelId,
                                                     bool useCookiesFile = false,
                                                     CancellationToken cancellation = default);
}
