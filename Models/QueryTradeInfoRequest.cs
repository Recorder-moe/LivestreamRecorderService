using FluentEcpay;

namespace LivestreamRecorderService.Models;

internal class QueryTradeInfoRequest
{
    public required string MerchantID { get; set; }
    public required string MerchantTradeNo { get; set; }
    public string? PlatformID { get; set; }

    public int TimeStamp { get; set; } = Convert.ToInt32(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

    public string? CheckMacValue { get; set; }
}
