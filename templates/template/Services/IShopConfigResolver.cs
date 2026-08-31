using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using McpGateway.__Department__.Models;

namespace McpGateway.__Department__.Services;

/// <summary>
/// 廠別與 API 路由解析服務介面
/// </summary>
public interface IShopConfigResolver
{
    /// <summary>
    /// 依據輸入之廠別名稱或別名解析目標廠別與 API 路由設定
    /// </summary>
    /// <param name="shop">廠別代碼或別名 (例如 CF3, TFT7, ARY7, CEL7, RDL1)</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>解析後的標準廠別設定</returns>
    /// <exception cref="McpGateway.__Department__.Exceptions.ShopNotFoundException">查無廠別時拋出</exception>
    Task<ResolvedShopConfig> ResolveShopAsync(string shop, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得所有已載入之標準廠別清單
    /// </summary>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>廠別清單</returns>
    Task<IReadOnlyList<ConsulShopItem>> GetAllShopsAsync(CancellationToken cancellationToken = default);
}
