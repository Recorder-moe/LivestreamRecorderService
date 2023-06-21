using k8s;
using k8s.Models;
using LivestreamRecorder.DB.Enum;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
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

    public abstract string Name { get; }
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

    public virtual async Task InitJobAsync(string videoId, Video video, bool useCookiesFile = false, CancellationToken cancellation = default)
    {
        var jobName = GetInstanceName(videoId);

        var job = await GetJobByKeywordAsync(jobName, cancellation);
        if (null != job && job.Status.Active != 0)
        {
            _logger.LogWarning("An already active job found for {videoId} {name}!! A new job will now be created with different name. Please pay attention if this happens repeatedly.", videoId, jobName);
            jobName += DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        _logger.LogInformation("Start new K8s job for {videoId} {name}.", videoId, jobName);
        await CreateNewJobAsync(id: videoId,
                                instanceName: jobName,
                                video: video,
                                useCookiesFile: useCookiesFile,
                                cancellation: cancellation);
    }

    protected async Task<V1Job?> GetJobByKeywordAsync(string keyword, CancellationToken cancellation)
    {
        var jobs = await _client.ListNamespacedJobAsync(KubernetesNamespace, cancellationToken: cancellation);
        return jobs.Items.FirstOrDefault(p => p.Name().Contains(GetInstanceName(keyword)));
    }

    protected Task<V1Job> CreateInstanceAsync(dynamic parameters, string deploymentName, IDictionary<string, string>? environment = null, CancellationToken cancellation = default)
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

        if (null != environment && environment.Count > 0)
        {
            job.Spec.Template.Spec.Containers[0].Env = environment.Select(p => new V1EnvVar(p.Key, p.Value)).ToList();
        }

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
                Secret = new()
                {
                    SecretName = KubernetesService._nfsSecretName,
                }
            },
            _ => throw new ConfigurationErrorsException($"ShareVolume Service {Enum.GetName(typeof(ServiceName), _serviceOption.SharedVolumeService)} not supported for Kubernetes.")
        };

    public string GetInstanceName(string videoId)
        => (Name + NameHelper.GetInstanceName(videoId)).ToLower();

    // Must be override
    protected abstract Task<V1Job> CreateNewJobAsync(string id,
                                                     string instanceName,
                                                     Video video,
                                                     bool useCookiesFile = false,
                                                     CancellationToken cancellation = default);
}
