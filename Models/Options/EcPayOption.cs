namespace LivestreamRecorderService.Models.Options;

public sealed class EcPayOption
{
#pragma warning disable IDE1006 // 命名樣式
    public const string ConfigurationSectionName = "Ecpay";
#pragma warning restore IDE1006 // 命名樣式

    public required string MerchantID { get; set; }
    public required string HashKey { get; set; }
    public required string HashIV { get; set; }
 }

