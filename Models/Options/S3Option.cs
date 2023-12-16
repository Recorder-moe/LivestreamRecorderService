using System.ComponentModel.DataAnnotations;

namespace LivestreamRecorderService.Models.Options;

public sealed class S3Option
{
#pragma warning disable IDE1006 // 命名樣式
    public const string ConfigurationSectionName = "S3";
#pragma warning restore IDE1006 // 命名樣式

    [Required]
    public string Endpoint { get; set; } = "";
    public bool Secure { get; set; } = true;
    [Required]
    public string AccessKey { get; set; } = "";
    [Required]
    public string SecretKey { get; set; } = "";
    [Required]
    public string BucketName_Private { get; set; } = "";
    [Required]
    public string BucketName_Public { get; set; } = "";
    [Required]
    public int RetentionDays { get; set; } = 4;
}
