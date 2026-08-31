namespace McpGateway.__Department__.Models;

/// <summary>
/// 解析完成之標準廠別與 API 路由設定
/// </summary>
/// <param name="ShopId">標準化廠別代碼 (例如 CF3, TFT7, RDL1)</param>
/// <param name="Fab">所屬廠區代碼 (例如 FAB3, FAB7, FAB1)</param>
/// <param name="WipCode">部門 API 使用之廠別代碼 (例如 CF3, TFT7, RDL1)</param>
/// <param name="Category">廠別分類 (例如 cf, tft, rdl, lcd, mod)</param>
/// <param name="DllShopName">DLL 廠別名稱 (例如 Cf3, Tft7, Rdl1)</param>
/// <param name="EnvironmentType">環境類型 (例如 production, test)</param>
public sealed record ResolvedShopConfig(
    string ShopId,
    string Fab,
    string WipCode,
    string Category,
    string DllShopName,
    string EnvironmentType
);
