namespace LivestreamRecorderService.Models.Options;

public class NFSOption
{
#pragma warning disable IDE1006 // 命名樣式
    public const string ConfigurationSectionName = "NFS";
#pragma warning restore IDE1006 // 命名樣式

    public string? Server { get; set; }
    public string? Path { get; set; }
}
