using k8s;
using k8s.Models;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Interfaces.Job;
using LivestreamRecorderService.Models;
using System.Globalization;

namespace LivestreamRecorderService.SingletonServices.Kubernetes;

public abstract class KubernetesServiceBase(
    ILogger<KubernetesServiceBase> logger,
    k8s.Kubernetes kubernetes,
    IUploaderService uploaderService) : IJobServiceBase
{
    public abstract string Name { get; }
    private static string KubernetesNamespace => KubernetesService.KubernetesNamespace;

    private const string DefaultRegistry = "ghcr.io/recorder-moe/";
    private const string FallbackRegistry = "recordermoe/";

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

    protected Task<V1Job> CreateInstanceAsync(string deploymentName,
                                              string containerName,
                                              string imageName,
                                              string[] args,
                                              string fileName,
                                              string[]? command = default,
                                              IList<EnvironmentVariable>? environment = null,
                                              string mountPath = "/sharedvolume",
                                              bool fallback = false,
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
                Template = new V1PodTemplateSpec
                {
                    Spec = new V1PodSpec
                    {
                        RestartPolicy = "OnFailure",
                        Volumes = new List<V1Volume>
                        {
                            new()
                            {
                                Name = "sharedvolume",
                                EmptyDir = new V1EmptyDirVolumeSource()
                            }
                        },
                        // Downloader container
                        InitContainers = new List<V1Container>
                        {
                            new()
                            {
                                Name = containerName,
                                Image = fallback switch
                                {
                                    false => DefaultRegistry + imageName,
                                    true => FallbackRegistry + imageName
                                },
                                Args = args,
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
                        // Uploader container
                        Containers = new List<V1Container>
                        {
                            new()
                            {
                                Name = containerName + "-uploader",
                                Image = fallback switch
                                {
                                    false => DefaultRegistry + uploaderService.Image,
                                    true => FallbackRegistry + uploaderService.Image
                                },
                                Args = [Path.Combine(mountPath, fileName.Replace(".mp4", ""))],
                                VolumeMounts = new List<V1VolumeMount>
                                {
                                    new()
                                    {
                                        Name = "sharedvolume",
                                        MountPath = mountPath,
                                    }
                                },
                                Env = uploaderService.GetEnvironmentVariables()
                                                     .Select(p => new V1EnvVar(p.Name, p.Value ?? p.SecureValue))
                                                     .ToList(),
                            }
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

        if (null != command && command.Length > 0)
        {
            job.Spec.Template.Spec.Containers[0].Command = command;
        }

        return kubernetes.CreateNamespacedJobAsync(body: job,
                                                   namespaceParameter: KubernetesNamespace,
                                                   cancellationToken: cancellation);
    }

    public string GetInstanceName(string videoId)
        => (Name + NameHelper.GetInstanceName(videoId)).ToLower(CultureInfo.InvariantCulture);

    // Must be overridden
    protected abstract Task<V1Job> CreateNewJobAsync(string id,
        string instanceName,
        Video video,
        bool useCookiesFile = false,
        CancellationToken cancellation = default);
}
