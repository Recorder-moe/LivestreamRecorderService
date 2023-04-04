using FluentEcpay;
using LivestreamRecorder.DB.Enum;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Models;
using LivestreamRecorderService.Models.Options;
using Microsoft.Extensions.Options;
using Omu.ValueInjecter;
using System.Collections.Specialized;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Web;

namespace LivestreamRecorderService.SingletonServices;

internal class EcPayService
{
    private readonly ILogger<EcPayService> _logger;
    private readonly IHttpClientFactory _httpFactory;
    private readonly string _hashKey;
    private readonly string _hashIV;
    private readonly string _merchantID;
#if DEBUG
    private const string _ecPayQueryTradeInfoApi = "https://payment-stage.ecpay.com.tw/Cashier/QueryTradeInfo/V5";
#else
    private const string _ecPayQueryTradeInfoApi = "https://payment.ecpay.com.tw/Cashier/QueryTradeInfo/V5";
#endif

    public EcPayService(
        ILogger<EcPayService> logger,
        IHttpClientFactory httpFactory,
        IOptions<EcPayOption> options)
    {
        _logger = logger;
        _httpFactory = httpFactory;
        _hashKey = options.Value.HashKey;
        _hashIV = options.Value.HashIV;
        _merchantID = options.Value.MerchantID;
    }

    internal T SetCheckMac<T>(T obj)
    {
        if (obj is null) throw new ArgumentNullException(nameof(obj));

        var properties = typeof(T).GetProperties();
        var parameters = properties
            .Where(property => property.Name != "URL")
            .Where(property => property.GetValue(obj) != null)
            .ToDictionary(property => property.Name, property => property.GetValue(obj)?.ToString());
        var checkmac = CheckMac.GetValue(parameters, _hashKey, _hashIV);

        var checkMacProperty = typeof(T).GetProperty("CheckMacValue");
        checkMacProperty?.SetValue(obj, checkmac);
        return obj;
    }

    public bool ResultIsValid<T>(T result)
    {
        if (result is null) throw new ArgumentNullException(nameof(result));

        var properties = typeof(T).GetProperties();
        var parameters = properties
            .Where(property => property.Name != "CheckMacValue")
            .Where(p => null != p.GetValue(result))
            .ToDictionary(property => property.Name, property =>
            {
                var value = property.GetValue(result);
                return value?.ToString();
            });
        var toCheck = CheckMac.GetValue(parameters, _hashKey, _hashIV);
        return toCheck == typeof(T).GetProperty("CheckMacValue")?.GetValue(result)?.ToString();
    }

    internal async Task<(TransactionState, string?)?> UpdateEcPayTradeResultAsync(Transaction transaction, CancellationToken cancellation)
    {
        try
        {
            using var client = _httpFactory.CreateClient();
            QueryTradeInfoRequest queryTradeInfoRequest = new()
            {
                MerchantID = _merchantID,
                MerchantTradeNo = transaction.EcPayMerchantTradeNo!,
            };
            queryTradeInfoRequest = SetCheckMac(queryTradeInfoRequest);

            // https://github.dev/cjyuzzi/FluentEcpay/blob/master/FluentEcpay.Test/FluentEcpayTest_Payment.cs#L193
            var properties = typeof(QueryTradeInfoRequest).GetProperties();
            var parameters = properties
                .Where(property => property.GetValue(queryTradeInfoRequest) != null)
                .ToDictionary(property => property.Name, property => property.GetValue(queryTradeInfoRequest)!.ToString());
            var content = new FormUrlEncodedContent(parameters);
            var response = await client.PostAsync(_ecPayQueryTradeInfoApi, content, cancellation);
            response.EnsureSuccessStatusCode();

            var resultString = await response.Content.ReadAsStringAsync(cancellation);
            NameValueCollection collection = HttpUtility.ParseQueryString(resultString);

            var res = Activator.CreateInstance<QueryTradeInfoResponse>();

            foreach (var key in collection.AllKeys)
            {
                if (string.IsNullOrEmpty(key) || null == collection[key]) continue;

                var propertyInfo = typeof(QueryTradeInfoResponse).GetProperty(key);
                if (propertyInfo != null)
                {
                    string value = collection[key]!;
                    object convertedValue = Convert.ChangeType(value, propertyInfo.PropertyType);
                    propertyInfo.SetValue(res, convertedValue, null);
                }
            }

            if (null == res || !ResultIsValid(res))
            {
                _logger.LogWarning("EcPay does not return a valid response. {transactionId}", transaction.id);
                return null;
            }

            switch (res.TradeStatus)
            {
                case "0":
                    // 若為0時，代表交易訂單成立未付款
                    _logger.LogTrace("EcPay trade result is Unpaid(0). {transactionId}", transaction.id);
                    return (TransactionState.Pending, res.TradeNo);
                case "1":
                    // 若為1時，代表交易訂單成立已付款
                    _logger.LogTrace("EcPay trade result is Paid(1). {transactionId}", transaction.id);

                    // 部份錯誤狀態不會回傳CustomField，故只在成功時做檢核
                    if (res.CustomField1 != transaction.id || res.CustomField2 != transaction.UserId)
                    {
                        _logger.LogWarning("EcPay does not return a response which matches our transaction data. {transactionId}", transaction.id);
                        return null;
                    }

                    return (TransactionState.Success, res.TradeNo);
                case "10200095":
                    // 若為 10200095時，代表交易訂單未成立，消費者未完成付款作業，故交易失敗。
                    _logger.LogTrace("EcPay trade result is Not established(10200095). {transactionId}", transaction.id);
                    return (TransactionState.Cancel, null);
                case "10200047":
                    // Cant not find the trade data.
                    _logger.LogTrace("EcPay trade result is Not found(10200047). {transactionId}", transaction.id);
                    return (TransactionState.Cancel, null);
                default:
                    _logger.LogTrace("EcPay trade result is out of handled! {result} {transactionId}", res.TradeStatus, transaction.id);
                    return null;
            }
        }
        catch (Exception)
        {
            _logger.LogError("Update EcPay trade result failed. {transactionId}", transaction.id);
            return null;
        }
    }

}
