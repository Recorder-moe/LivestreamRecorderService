using k8s;
using k8s.Models;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Enums;
using LivestreamRecorderService.Helper;
using LivestreamRecorderService.Interfaces.Job;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;
using System.Configuration;

namespace LivestreamRecorderService.SingletonServices.Kubernetes;

public class KubernetesService : IJobService
{
    private readonly ILogger<KubernetesService> _logger;
    private readonly k8s.Kubernetes _client;
    private readonly AzureOption _azureOption;
    private readonly ServiceOption _serviceOption;
    internal const string _azureFileShareSecretName = "azure-fileshare-secret";
    internal static string _persistentVolumeClaimName = "";

    internal static string KubernetesNamespace { get; set; } = "recordermoe";

    public KubernetesService(
        ILogger<KubernetesService> logger,
        k8s.Kubernetes kubernetes,
        IOptions<AzureOption> azureOptions,
        IOptions<ServiceOption> serviceOptions,
        IOptions<KubernetesOption> options)
    {
        _logger = logger;
        _client = kubernetes;
        _azureOption = azureOptions.Value;
        _serviceOption = serviceOptions.Value;

        KubernetesNamespace = options.Value.Namespace ?? KubernetesNamespace;
        EnsureNamespaceExists(KubernetesNamespace);

        if (_serviceOption.SharedVolumeService == ServiceName.AzureFileShare
            && !CheckSecretExists())
            CreateSecret();

        if (_serviceOption.SharedVolumeService == ServiceName.CustomPVC)
        {
            _persistentVolumeClaimName = options.Value.PVCName!;

            if (!CheckPersistentVolumeClaimExists())
            {
                _logger.LogCritical("PresistentVolumeClaim {name} does not exist!", _persistentVolumeClaimName);
                throw new ConfigurationErrorsException($"PresistentVolumeClaim {_persistentVolumeClaimName} does not exist!");
            }
        }
    }

    public Task<bool> IsJobSucceededAsync(Video video, CancellationToken cancellation = default)
        => IsJobSucceededAsync(NameHelper.GetInstanceName(video.id), cancellation);

    public async Task<bool> IsJobSucceededAsync(string keyword, CancellationToken cancellation = default)
        => (await GetJobsByKeywordAsync(keyword, cancellation))
                    .Any(job => (job.Status.Active is null or 0)
                                 && job.Status.Succeeded > 0);

    public Task<bool> IsJobFailedAsync(Video video, CancellationToken cancellation = default)
        => IsJobFailedAsync(NameHelper.GetInstanceName(video.id), cancellation);

    public async Task<bool> IsJobFailedAsync(string keyword, CancellationToken cancellation = default)
        => (await GetJobsByKeywordAsync(keyword, cancellation))
                    .Any(job => (job.Status.Active is null or 0)
                                && job.Status.Failed > 0
                                && (job.Status.Succeeded is null or 0));

    public async Task RemoveCompletedJobsAsync(Video video, CancellationToken cancellation = default)
    {
        var jobs = (await GetJobsByKeywordAsync(video.id, cancellation)).Where(p => p.Status.Conditions.LastOrDefault()?.Type == "Complete").ToList();
        if (jobs.Count == 0)
        {
            _logger.LogError("Failed to retrieve K8s job for {videoId} while removing completed job. Please verify if any job exists.", video.id);
            throw new InvalidOperationException($"No K8s jobs found! {video.id}");
        }

        if (jobs.Count > 1)
        {
            _logger.LogWarning("Multiple jobs were found for {videoId} while removing the COMPLETED job. This should not occur in the normal process, but we will take care of cleaning them up.", video.id);
        }

        foreach (var job in jobs)
        {
            string jobName = job.Name();
            if (await IsJobFailedAsync(video, cancellation))
            {
                _logger.LogError("K8s job status FAILED! {videoId} {jobName}", video.id, jobName);
                throw new InvalidOperationException($"K8s job status FAILED! {jobName}");
            }

            var status = await _client.DeleteNamespacedJobAsync(name: jobName,
                                                                namespaceParameter: job.Namespace(),
                                                                propagationPolicy: "Background",
                                                                cancellationToken: cancellation);
            if (status.Status != "Success")
            {
                _logger.LogError("Failed to delete job {jobName} {videoId} {status}", jobName, video.id, status.Message);
                throw new InvalidOperationException($"Failed to delete job {jobName} {video.id} {status.Message}");
            }
            _logger.LogInformation("K8s job {jobName} {videoId} removed", jobName, video.id);
        }
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

    private bool CheckPersistentVolumeClaimExists()
        => _client.ListNamespacedPersistentVolumeClaim(KubernetesNamespace)
                  .Items
                  .Any(p => p.Name() == _persistentVolumeClaimName);

    private async Task<List<V1Job>> GetJobsByKeywordAsync(string keyword, CancellationToken cancellation)
    {
        var jobs = await _client.ListNamespacedJobAsync(KubernetesNamespace, cancellationToken: cancellation);
        return jobs.Items.Where(p => p.Name().Contains(NameHelper.GetInstanceName(keyword))).ToList();
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
