using k8s;
using k8s.Models;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Enums;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces.Job;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;

namespace LivestreamRecorderService.SingletonServices.Kubernetes;

public class KubernetesService : IJobService
{
    private readonly ILogger<KubernetesService> _logger;
    private readonly k8s.Kubernetes _client;
    private readonly AzureOption _azureOption;
    private readonly ServiceOption _serviceOption;
    private readonly NFSOption _nfsOption;
    internal const string _azureFileShareSecretName = "azure-fileshare-secret";
    internal const string _storageClassName = "nfs-csi";
    internal static string _persistentVolumeClaimName = "";

    internal static string KubernetesNamespace { get; set; } = "recordermoe";

    public KubernetesService(
        ILogger<KubernetesService> logger,
        k8s.Kubernetes kubernetes,
        IOptions<AzureOption> azureOptions,
        IOptions<ServiceOption> serviceOptions,
        IOptions<KubernetesOption> options,
        IOptions<NFSOption> nfsOptions)
    {
        _logger = logger;
        _client = kubernetes;
        _azureOption = azureOptions.Value;
        _serviceOption = serviceOptions.Value;
        _nfsOption = nfsOptions.Value;

        KubernetesNamespace = options.Value.Namespace ?? KubernetesNamespace;
        EnsureNamespaceExists(KubernetesNamespace);

        if (_serviceOption.SharedVolumeService == ServiceName.AzureFileShare)
        {
            if (!CheckSecretExists()) CreateSecret();
        }

        if (_serviceOption.SharedVolumeService == ServiceName.NFS)
        {
            _persistentVolumeClaimName = "nfs-pvc";

            if (!CheckStorageClassExists()) CreateNFSStorageClass();

            if (!CheckPersistentVolumeClaimExists()) CreatePersistentVolumeClaim();
        }

        if (_serviceOption.SharedVolumeService == ServiceName.CustomPVC)
        {
            _persistentVolumeClaimName = options.Value.PVCName!;

            if (!CheckPersistentVolumeClaimExists()) CreatePersistentVolumeClaim();
        }
    }

    public Task<bool> IsJobSucceededAsync(Video video, CancellationToken cancellation = default)
        => IsJobSucceededAsync(NameHelper.GetInstanceName(video.id), cancellation);

    public async Task<bool> IsJobSucceededAsync(string keyword, CancellationToken cancellation = default)
    {
        var job = await GetJobByKeywordAsync(keyword, cancellation);
        return null != job
               && (job.Status.Active == null || job.Status.Active == 0)
               && job.Status.Succeeded > 0;
    }

    public Task<bool> IsJobFailedAsync(Video video, CancellationToken cancellation = default)
        => IsJobFailedAsync(NameHelper.GetInstanceName(video.id), cancellation);

    public async Task<bool> IsJobFailedAsync(string keyword, CancellationToken cancellation = default)
    {
        var job = await GetJobByKeywordAsync(keyword, cancellation);
        return null == job || job.Status.Failed > 0;
    }

    public async Task RemoveCompletedJobsAsync(Video video, CancellationToken cancellation = default)
    {
        var job = await GetJobByKeywordAsync(video.id, cancellation);
        if (null == job)
        {
            _logger.LogError("Failed to get K8s job for {videoId} when removing completed job. Please check if the job exists.", video.id);
            return;
        }

        string jobName = job.Name();
        if (await IsJobFailedAsync(video, cancellation))
        {
            _logger.LogError("K8s job status FAILED! {videoId} {jobName}", video.id, jobName);
            throw new Exception($"K8s job status FAILED! {jobName}");
        }

        var status = await _client.DeleteNamespacedJobAsync(name: jobName,
                                                            namespaceParameter: job.Namespace(),
                                                            propagationPolicy: "Foreground",
                                                            cancellationToken: cancellation);
        if (status.Status == "Failure")
        {
            _logger.LogError("Failed to delete job {jobName} {videoId} {status}", jobName, video.id, status.Message);
            throw new Exception($"Failed to delete job {jobName} {video.id} {status.Message}");
        }
        _logger.LogInformation("K8s job {jobName} {videoId} removed", jobName, video.id);
    }

    private bool CheckSecretExists()
        => _client.ListNamespacedSecret(KubernetesNamespace)
                      .Items
                      .Any(secret => secret.Metadata.Name == _azureFileShareSecretName);

    private void CreateSecret()
    {
        var secret = new V1Secret
        {
            Metadata = new V1ObjectMeta
            {
                Name = _azureFileShareSecretName
            },
            StringData = new Dictionary<string, string>
            {
                ["azurestorageaccountname"] = _azureOption.FileShare!.StorageAccountName,
                ["azurestorageaccountkey"] = _azureOption.FileShare!.StorageAccountKey
            }
        };

        _client.CreateNamespacedSecret(secret, KubernetesNamespace);
    }

    private bool CheckStorageClassExists()
        => _client.ListStorageClass()
                  .Items
                  .Any(p => p.Name() == _storageClassName);

    private bool CheckPersistentVolumeClaimExists()
        => _client.ListNamespacedPersistentVolumeClaim(KubernetesNamespace)
                  .Items
                  .Any(p => p.Name() == _persistentVolumeClaimName);

    private void CreateNFSStorageClass()
    {
        var storageClass = new V1StorageClass
        {
            Metadata = new V1ObjectMeta
            {
                Name = _storageClassName
            },
            Provisioner = "nfs.csi.k8s.io",
            VolumeBindingMode = "Immediate",
            ReclaimPolicy = "Retain",
            MountOptions = new List<string> { "nfsvers=4.1" },
            Parameters = new Dictionary<string, string>
            {
                ["server"] = string.IsNullOrEmpty(_nfsOption.Server)
                                ? "nfs-server.recordermoe.svc.cluster.local"
                                : _nfsOption.Server,
                ["share"] = string.IsNullOrEmpty(_nfsOption.Path)
                                ? "/"
                                : _nfsOption.Path
            }
        };

        _client.CreateStorageClass(storageClass);
    }

    private void CreatePersistentVolumeClaim()
    {
        var pvc = new V1PersistentVolumeClaim
        {
            Metadata = new V1ObjectMeta
            {
                Name = _persistentVolumeClaimName
            },
            Spec = new V1PersistentVolumeClaimSpec
            {
                StorageClassName = _storageClassName,
                AccessModes = new List<string> { "ReadWriteOnce" },
                Resources = new V1ResourceRequirements
                {
                    Requests = new Dictionary<string, ResourceQuantity>
                    {
                        ["storage"] = new ResourceQuantity("10Gi")
                    }
                }
            }
        };

        _client.CreateNamespacedPersistentVolumeClaim(pvc, KubernetesNamespace);
    }

    private async Task<V1Job?> GetJobByKeywordAsync(string keyword, CancellationToken cancellation)
    {
        var jobs = await _client.ListNamespacedJobAsync(KubernetesNamespace, cancellationToken: cancellation);
        return jobs.Items.FirstOrDefault(p => p.Name().Contains(NameHelper.GetInstanceName(keyword)));
    }

    private void EnsureNamespaceExists(string namespaceName)
    {
        var existingNamespace = _client.ListNamespace().Items.ToList().Find(ns => ns.Metadata.Name == namespaceName);

        if (existingNamespace != null) return;

        var newNamespace = new V1Namespace()
        {
            Metadata = new V1ObjectMeta()
            {
                Name = namespaceName
            }
        };

        _client.CreateNamespace(newNamespace);
        _logger.LogInformation("Namespace {namespaceName} created.", namespaceName);
    }
}
