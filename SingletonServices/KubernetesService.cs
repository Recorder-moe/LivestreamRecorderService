using k8s;
using k8s.Models;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices;

public class KubernetesService(
    ILogger<KubernetesService> logger,
    Kubernetes kubernetes,
    IOptions<KubernetesOption> options,
    IUploaderService uploaderService) : IJobService
{
    private const string DefaultRegistry = "ghcr.io/recorder-moe/";
    private const string FallbackRegistry = "recordermoe/";

    private readonly string _kubernetesNamespace = options.Value.Namespace ?? "recordermoe";

    public Task<bool> IsJobMissing(Video video, CancellationToken cancellation)
    {
        return IsJobMissing(NameHelper.CleanUpInstanceName(video.id), cancellation);
    }

    public async Task<bool> IsJobMissing(string keyword, CancellationToken cancellation)
    {
        return (await GetJobsByKeywordAsync(keyword, cancellation)).Count == 0;
    }

    public Task<bool> IsJobSucceededAsync(Video video, CancellationToken cancellation = default)
    {
        return IsJobSucceededAsync(NameHelper.CleanUpInstanceName(video.id), cancellation);
    }

    public async Task<bool> IsJobSucceededAsync(string keyword, CancellationToken cancellation = default)
    {
        return (await GetJobsByKeywordAsync(keyword, cancellation))
            .Any(job => job.Status.Active is null or 0
                        && job.Status.Succeeded > 0);
    }

    public Task<bool> IsJobFailedAsync(Video video, CancellationToken cancellation = default)
    {
        return IsJobFailedAsync(NameHelper.CleanUpInstanceName(video.id), cancellation);
    }

    public async Task<bool> IsJobFailedAsync(string keyword, CancellationToken cancellation = default)
    {
        return (await GetJobsByKeywordAsync(keyword, cancellation))
            .Any(job => job.Status.Active is null or 0
                        && job.Status.Failed > 0);
    }

    public async Task RemoveCompletedJobsAsync(Video video, CancellationToken cancellation = default)
    {
        var jobs = (await GetJobsByKeywordAsync(video.id, cancellation)).Where(p => p.Status.Conditions.LastOrDefault()?.Type == "Complete").ToList();
        if (jobs.Count == 0)
        {
            logger.LogError("Failed to retrieve K8s job for {videoId} while removing completed job. Please verify if any job exists.", video.id);
            throw new InvalidOperationException($"No K8s jobs found! {video.id}");
        }

        if (jobs.Count > 1)
            logger.LogWarning(
                "Multiple jobs were found for {videoId} while removing the COMPLETED job. This should not occur in the normal process, but we will take care of cleaning them up.",
                video.id);

        foreach (V1Job? job in jobs)
        {
            string jobName = job.Name();
            if (await IsJobFailedAsync(video, cancellation))
            {
                logger.LogError("K8s job status FAILED! {videoId} {jobName}", video.id, jobName);
                throw new InvalidOperationException($"K8s job status FAILED! {jobName}");
            }

            V1Status? status = await kubernetes.DeleteNamespacedJobAsync(name: jobName,
                                                                         namespaceParameter: job.Namespace(),
                                                                         propagationPolicy: "Background",
                                                                         cancellationToken: cancellation);

            if (status.Status != "Success")
            {
                logger.LogError("Failed to delete job {jobName} {videoId} {status}", jobName, video.id, status.Message);
                throw new InvalidOperationException($"Failed to delete job {jobName} {video.id} {status.Message}");
            }

            logger.LogInformation("K8s job {jobName} {videoId} removed", jobName, video.id);
        }
    }

    public async Task CreateInstanceAsync(string deploymentName,
                                          string containerName,
                                          string imageName,
                                          string fileName,
                                          string[]? command = default,
                                          string[]? args = default,
                                          string mountPath = "/sharedvolume",
                                          CancellationToken cancellation = default)
    {
        if (null != command && command.Length == 0)
            throw new ArgumentNullException(nameof(command), "command can be null, but cannot be empty.");

        if ((null == command || command.Length == 0)
            && (null == args || args.Length == 0))
            throw new ArgumentNullException(nameof(args), "command and args cannot be empty at the same time.");

        V1Job? oldJob = await GetJobByKeywordAsync(containerName, cancellation);
        if (null != oldJob && oldJob.Status.Active != 0)
        {
            logger.LogError("An already active job found for {imageName}", imageName);
            throw new InvalidOperationException("An already active job found.");
        }

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
                            },
                            new()
                            {
                                Name = "cookies",
                                Secret = new V1SecretVolumeSource
                                {
                                    SecretName = "cookies",
                                    DefaultMode = 432, // octal 0660 to decimal
                                    Optional = true
                                }
                            }
                        },
                        // Downloader container
                        InitContainers = new List<V1Container>
                        {
                            new()
                            {
                                Name = containerName,
                                Image = DefaultRegistry + imageName,
                                // The args and commands will be set afterward
                                VolumeMounts = new List<V1VolumeMount>
                                {
                                    new()
                                    {
                                        Name = "sharedvolume",
                                        MountPath = mountPath
                                    },
                                    new()
                                    {
                                        Name = "cookies",
                                        MountPath = "/cookies"
                                    }
                                }
                            }
                        },
                        // Uploader container
                        Containers = new List<V1Container>
                        {
                            new()
                            {
                                Name = containerName + "-uploader",
                                Image = DefaultRegistry + uploaderService.Image,
                                Args = [fileName.Replace(".mp4", "")],
                                VolumeMounts = new List<V1VolumeMount>
                                {
                                    new()
                                    {
                                        Name = "sharedvolume",
                                        MountPath = "/sharedvolume"
                                    }
                                },
                                Env = uploaderService.GetEnvironmentVariables()
                                                     .Select(p => new V1EnvVar(p.Name, p.Value ?? p.SecureValue))
                                                     .ToList()
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

        // Add command if provided
        if (null != command && command.Length > 0) job.Spec.Template.Spec.InitContainers[0].Command = command;

        // Add args if provided, args can be empty
        if (null != args) job.Spec.Template.Spec.InitContainers[0].Args = args;

        try
        {
            await kubernetes.CreateNamespacedJobAsync(body: job,
                                                      namespaceParameter: _kubernetesNamespace,
                                                      cancellationToken: cancellation);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed once, try fallback registry.");
            job.Spec.Template.Spec.InitContainers[0].Image = FallbackRegistry + imageName;
            job.Spec.Template.Spec.Containers[0].Image = FallbackRegistry + imageName;

            try
            {
                await kubernetes.CreateNamespacedJobAsync(body: job,
                                                          namespaceParameter: _kubernetesNamespace,
                                                          cancellationToken: cancellation);
            }
            catch (Exception e2)
            {
                logger.LogError(e2, "Failed twice, abort.");
                throw;
            }
        }
    }

    private async Task<List<V1Job>> GetJobsByKeywordAsync(string keyword, CancellationToken cancellation)
    {
        V1JobList? jobs = await kubernetes.ListNamespacedJobAsync(_kubernetesNamespace, cancellationToken: cancellation);
        return jobs.Items.Where(p => p.Name().Contains(NameHelper.CleanUpInstanceName(keyword))).ToList();
    }

    private async Task<V1Job?> GetJobByKeywordAsync(string keyword, CancellationToken cancellation)
    {
        V1JobList? jobs = await kubernetes.ListNamespacedJobAsync(_kubernetesNamespace, cancellationToken: cancellation);
        return jobs.Items.FirstOrDefault(p => p.Name().Contains(keyword));
    }
}
