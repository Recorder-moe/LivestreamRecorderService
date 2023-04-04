namespace LivestreamRecorderService.Models;

internal class QueryTradeInfoResponse
{
    public required string MerchantID { get; set; }
    public required string MerchantTradeNo { get; set; }
    public string? StoreID { get; set; }
    public required string TradeNo { get; set; }
    public int TradeAmt { get; set; }
    public required string PaymentDate { get; set; }
    public required string PaymentType { get; set; }
    public int HandlingCharge { get; set; }
    public int PaymentTypeChargeFee { get; set; }
    public required string TradeDate { get; set; }
    public required string TradeStatus { get; set; }
    public string? ItemName { get; set; }
    public string? CustomField1 { get; set; }
    public string? CustomField2 { get; set; }
    public string? CustomField3 { get; set; }
    public string? CustomField4 { get; set; }
    public required string CheckMacValue { get; set; }
}
