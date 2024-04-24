using k8s;
using k8s.Models;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Enums;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces.Job;
using LivestreamRecorderService.Models;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;
using System.Configuration;
using System.Globalization;

namespace LivestreamRecorderService.SingletonServices.Kubernetes;

public abstract class KubernetesServiceBase(
    ILogger<KubernetesServiceBase> logger,
    k8s.Kubernetes kubernetes,
    IOptions<ServiceOption> serviceOptions,
    IOptions<AzureOption> azureOptions) : IJobServiceBase
{
    private readonly ServiceOption _serviceOption = serviceOptions.Value;
    private readonly AzureOption _azureOption = azureOptions.Value;

    public abstract string Name { get; }
    private static string KubernetesNamespace => KubernetesService.KubernetesNamespace;

    public virtual async Task InitJobAsync(string videoId, Video video, bool useCookiesFile = false, CancellationToken cancellation = default)
    {
        string jobName = GetInstanceName(video.id);

        V1Job? job = await GetJobByKeywordAsync(jobName, cancellation);
        if (null != job && job.Status.Active != 0)
        {
            logger.LogWarning(
                "An already active job found for {videoId} {name}!! A new job will now be created with different name. Please pay attention if this happens repeatedly.",
                videoId,
                jobName);

            jobName += DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        logger.LogInformation("Start new K8s job for {videoId} {name}.", videoId, jobName);
        await CreateNewJobAsync(id: videoId,
            instanceName: jobName,
            video: video,
            useCookiesFile: useCookiesFile,
            cancellation: cancellation);
    }

    private async Task<V1Job?> GetJobByKeywordAsync(string keyword, CancellationToken cancellation)
    {
        V1JobList? jobs = await kubernetes.ListNamespacedJobAsync(KubernetesNamespace, cancellationToken: cancellation);
        return jobs.Items.FirstOrDefault(p => p.Name().Contains(keyword));
    }

    protected Task<V1Job> CreateInstanceAsync(dynamic parameters,
        string deploymentName,
        IList<EnvironmentVariable>? environment = null,
        string mountPath = "/sharedvolume",
        CancellationToken cancellation = default)
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
                            new()
                            {
                                Name = parameters.containerName.value,
                                Image = parameters.dockerImageName.value,
                                Command = parameters.commandOverrideArray.value,
                                VolumeMounts = new List<V1VolumeMount>
                                {
                                    new()
                                    {
                                        Name = "sharedvolume",
                                        MountPath = mountPath,
                                    }
                                },
                            }
                        },
                        Volumes = new List<V1Volume>
                        {
                            GetSharedVolumeDefinition()
                        },
                        SecurityContext = new V1PodSecurityContext
                        {
                            RunAsUser = 1001,
                            RunAsGroup = 0,
                            FsGroup = 0
                        }
                    }
                }
            }
        };

        if (null != environment && environment.Count > 0)
        {
            job.Spec.Template.Spec.Containers[0].Env = environment.Select(p => new V1EnvVar(p.Name, p.Value ?? p.SecureValue)).ToList();
        }

        return kubernetes.CreateNamespacedJobAsync(body: job,
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
                    SecretName = KubernetesService.AzureFileShareSecretName,
                    ShareName = _azureOption.FileShare!.ShareName,
                    ReadOnlyProperty = false,
                }
            },
            ServiceName.CustomPVC => new()
            {
                Name = "sharedvolume",
                PersistentVolumeClaim = new()
                {
                    ClaimName = KubernetesService.PersistentVolumeClaimName,
                    ReadOnlyProperty = false
                }
            },
            _ => throw new ConfigurationErrorsException(
                $"ShareVolume Service {Enum.GetName(typeof(ServiceName), _serviceOption.SharedVolumeService)} not supported for Kubernetes.")
        };

    public string GetInstanceName(string videoId)
        => (Name + NameHelper.GetInstanceName(videoId)).ToLower(CultureInfo.InvariantCulture);

    // Must be override
    protected abstract Task<V1Job> CreateNewJobAsync(string id,
        string instanceName,
        Video video,
        bool useCookiesFile = false,
        CancellationToken cancellation = default);
}
