using System;

namespace McpGateway.__Department__.Exceptions;

/// <summary>
/// 當指定之廠別或別名無法在 Consul 或本地組態中找到時拋出
/// </summary>
public class ShopNotFoundException : Exception
{
    public string RequestedShop { get; }

    public ShopNotFoundException(string requestedShop)
        : base($"找不到廠別 '{requestedShop}' 的組態設定。請確認廠別代碼或別名是否正確。")
    {
        RequestedShop = requestedShop;
    }

    public ShopNotFoundException(string requestedShop, string message)
        : base(message)
    {
        RequestedShop = requestedShop;
    }

    public ShopNotFoundException(string requestedShop, string message, Exception innerException)
        : base(message, innerException)
    {
        RequestedShop = requestedShop;
    }
}
