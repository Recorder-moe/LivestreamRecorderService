using System.ComponentModel.DataAnnotations;

// ReSharper disable InconsistentNaming

namespace LivestreamRecorderService.Models.Options;

public sealed class S3Option
{
    public const string ConfigurationSectionName = "S3";

    [Required] public string Endpoint { get; set; } = "";
    public bool Secure { get; set; } = true;
    [Required] public string AccessKey { get; set; } = "";
    [Required] public string SecretKey { get; set; } = "";
    [Required] public string BucketName_Private { get; set; } = "";
    [Required] public string BucketName_Public { get; set; } = "";
    [Required] public int RetentionDays { get; set; } = 4;
}
