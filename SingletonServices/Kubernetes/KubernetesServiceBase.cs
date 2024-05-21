using System.Globalization;
using k8s;
using k8s.Models;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces;
using LivestreamRecorderService.Interfaces.Job;

namespace LivestreamRecorderService.SingletonServices.Kubernetes;

public abstract class KubernetesServiceBase(
    ILogger<KubernetesServiceBase> logger,
    k8s.Kubernetes kubernetes,
    IUploaderService uploaderService) : IJobServiceBase
{
    private const string DefaultRegistry = "ghcr.io/recorder-moe/";
    private const string FallbackRegistry = "recordermoe/";
    private static string KubernetesNamespace => KubernetesService.KubernetesNamespace;

    /// <inheritdoc />
    public abstract string Name { get; }

    public string GetInstanceName(string videoId)
    {
        return (Name + NameHelper.GetInstanceName(videoId)).ToLower(CultureInfo.InvariantCulture);
    }

    // Must be overridden
    public abstract Task CreateJobAsync(Video video,
                                        bool useCookiesFile = false,
                                        string? url = null,
                                        CancellationToken cancellation = default);

    private async Task<V1Job?> GetJobByKeywordAsync(string keyword, CancellationToken cancellation)
    {
        V1JobList? jobs = await kubernetes.ListNamespacedJobAsync(KubernetesNamespace, cancellationToken: cancellation);
        return jobs.Items.FirstOrDefault(p => p.Name().Contains(keyword));
    }

    /// <summary>
    ///     Create Instance. Cookies file will be mounted at /cookies if K8s secrets exists.
    /// </summary>
    /// <param name="deploymentName">
    ///     Should be unique. Must consist of lower case alphanumeric characters or '-', and must
    ///     start and end with an alphanumeric character (e.g. 'my-name',  or '123-abc', regex used for validation is
    ///     '[a-z0-9]([-a-z0-9]*[a-z0-9])?')
    /// </param>
    /// <param name="containerName">
    ///     Should be unique. Must consist of lower case alphanumeric characters or '-', and must start
    ///     and end with an alphanumeric character (e.g. 'my-name',  or '123-abc', regex used for validation is
    ///     '[a-z0-9]([-a-z0-9]*[a-z0-9])?')
    /// </param>
    /// <param name="imageName">Download container image with tag. (e.g. 'yt-dlp:latest')</param>
    /// <param name="fileName">Recording file name.</param>
    /// <param name="command">
    ///     Override command for the download container. The original ENTRYPOINT will be used if not
    ///     provided.
    /// </param>
    /// <param name="args">
    ///     Override args for the download container. The original CMD will be used if not provided. Both
    ///     <paramref name="command" /> and <paramref name="args" /> cannot be empty at the same time.
    /// </param>
    /// <param name="mountPath">The mount path of the download container.</param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    protected async Task CreateInstanceAsync(string deploymentName,
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
                                                      namespaceParameter: KubernetesNamespace,
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
                                                          namespaceParameter: KubernetesNamespace,
                                                          cancellationToken: cancellation);
            }
            catch (Exception e2)
            {
                logger.LogError(e2, "Failed twice, abort.");
                throw;
            }
        }
    }
}
