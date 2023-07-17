namespace LivestreamRecorderService.Models.Options;

public class KubernetesOption
{
#pragma warning disable IDE1006 // 命名樣式
    public const string ConfigurationSectionName = "Kubernetes";
#pragma warning restore IDE1006 // 命名樣式

    public required bool UseTheSameCluster { get; set; } = true;
    public string? ConfigFile { get; set; }
    public string? Namespace { get; set; } = "recorder.moe";
    public string? PVCName { get; set; }
}
