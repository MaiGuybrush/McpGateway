using System.Threading;
using System.Threading.Tasks;
using McpGateway.__Department__.Tools.__ToolClass__;

namespace McpGateway.__Department__.Services;

/// <summary>
/// __ToolClass__ 服務介面
/// </summary>
public interface I__ToolClass__Service
{
    /// <summary>
    /// 執行 __ToolClass__ 查詢
    /// </summary>
    /// <param name="input">輸入參數</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>查詢結果</returns>
    Task<__ToolClass__ResultDto> ExecuteAsync(__ToolClass__Input input, CancellationToken cancellationToken = default);
}
