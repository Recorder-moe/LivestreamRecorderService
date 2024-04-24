using System.ComponentModel.DataAnnotations;

// ReSharper disable InconsistentNaming

namespace LivestreamRecorderService.Models.Options;

public class KubernetesOption
{
    public const string ConfigurationSectionName = "Kubernetes";

    [Required] public bool UseTheSameCluster { get; set; } = true;
    public string? Namespace { get; set; } = "recorder.moe";
    public string? ConfigFile { get; set; }
    public string? PVCName { get; set; }
}
