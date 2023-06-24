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
    internal const string _nfsSecretName = "nfs-secret";

    internal static string KubernetesNamespace { get; set; } = "recorder.moe";

    public KubernetesService(
        ILogger<KubernetesService> logger,
        k8s.Kubernetes kubernetes,
        IOptions<AzureOption> azureOptions,
        IOptions<ServiceOption> serviceOptions,
        IOptions<NFSOption> nfsOptions)
    {
        _logger = logger;
        _client = kubernetes;
        _azureOption = azureOptions.Value;
        _serviceOption = serviceOptions.Value;
        _nfsOption = nfsOptions.Value;

        if (!CheckSecretExists()) CreateSecret();
    }

    public Task<bool> IsJobSucceededAsync(Video video, CancellationToken cancellation = default)
        => IsJobSucceededAsync(NameHelper.GetInstanceName(video.id), cancellation);

    public async Task<bool> IsJobSucceededAsync(string keyword, CancellationToken cancellation = default)
    {
        var job = await GetJobByKeywordAsync(keyword, cancellation);
        return null != job && job.Status.Active == 0 && job.Status.Succeeded > 0;
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
                                                            cancellationToken: cancellation);
        if (status.Status == "Failure")
        {
            _logger.LogError("Failed to delete job {jobName} {videoId} {status}", jobName, video.id, status.Message);
            throw new Exception($"Failed to delete job {jobName} {video.id} {status.Message}");
        }
        _logger.LogInformation("K8s job {jobName} {videoId} removed", jobName, video.id);
    }

    private bool CheckSecretExists()
        => _serviceOption.SharedVolumeService switch
        {
            ServiceName.AzureFileShare => _client.ListNamespacedSecret(KubernetesNamespace)
                                                 .Items
                                                 .Any(secret => secret.Metadata.Name == _azureFileShareSecretName),
            ServiceName.NFS => _client.ListNamespacedSecret(KubernetesNamespace)
                                      .Items
                                      .Any(secret => secret.Metadata.Name == _nfsSecretName),
            _ => false,
        };

    private void CreateSecret()
    {
        var secret = _serviceOption.SharedVolumeService switch
        {
            ServiceName.NFS => new V1Secret
            {
                Metadata = new V1ObjectMeta
                {
                    Name = _nfsSecretName
                },
                StringData = new Dictionary<string, string>
                {
                    ["username"] = _nfsOption.Username ?? "",
                    ["password"] = _nfsOption.Password ?? ""
                }
            },
            ServiceName.AzureFileShare => new V1Secret
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
            },
            _ => throw new NotImplementedException(),
        };

        _client.CreateNamespacedSecret(secret, KubernetesNamespace);
    }

    private async Task<V1Job?> GetJobByKeywordAsync(string keyword, CancellationToken cancellation)
    {
        var jobs = await _client.ListNamespacedJobAsync(KubernetesNamespace, cancellationToken: cancellation);
        return jobs.Items.FirstOrDefault(p => p.Name().Contains(NameHelper.GetInstanceName(keyword)));
    }
}
