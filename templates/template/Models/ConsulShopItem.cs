using System.ComponentModel;
using System.Text.Json.Serialization;

namespace McpGateway.__Department__.Models;

/// <summary>
/// 企業 Consul K/V 'ShopList' 標準廠別設定項目
/// </summary>
public sealed class ConsulShopItem
{
    [Description("廠別唯一代碼 (Shop ID)，例如 CF3, TFT7, RDL1")]
    [JsonPropertyName("iD")]
    public string Id { get; set; } = string.Empty;

    [Description("所屬廠區代碼 (Fab)，例如 FAB1, FAB3, FAB7")]
    [JsonPropertyName("fab")]
    public string Fab { get; set; } = string.Empty;

    [Description("廠別類別 (Category)，例如 cf, tft, rdl, lcd")]
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [Description("DLL 廠別名稱 (DllShopName)，例如 Cf3, Tft7, Rdl1")]
    [JsonPropertyName("dllShopName")]
    public string DllShopName { get; set; } = string.Empty;

    [Description("環境類型 (EnvironmentType)，例如 production, test")]
    [JsonPropertyName("environmentType")]
    public string EnvironmentType { get; set; } = string.Empty;
}
