namespace LivestreamRecorderService.Models.Options;

public sealed class S3Option
{
#pragma warning disable IDE1006 // 命名樣式
    public const string ConfigurationSectionName = "S3";
#pragma warning restore IDE1006 // 命名樣式

    public required string Endpoint { get; set; }
    public bool Secure { get; set; } = true;
    public required string AccessKey { get; set; }
    public required string SecretKey { get; set; }
    public required string BucketName_Private { get; set; }
    public required string BucketName_Public { get; set; }
    public required int RetentionDays { get; set; }
}
