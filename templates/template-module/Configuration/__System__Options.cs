using System.Collections.Generic;

namespace McpGateway.__Department__.__System__.Configuration;

/// <summary>
/// __System__ 子系統專屬組態選項。
/// </summary>
public class __System__Options
{
    public const string SectionName = "McpGateway:Systems:__system__";

    /// <summary>
    /// 下游業務 API 相關設定。
    /// </summary>
    public DownstreamOptions Downstream { get; set; } = new();

    /// <summary>
    /// Consul 服務發現與 Key-Value 查詢設定。
    /// </summary>
    public ConsulOptions Consul { get; set; } = new();
}

public class DownstreamOptions
{
    /// <summary>
    /// 範例 1：靜態網址或本地 Fallback 網址（例如 http://api.corp.local/__department__/__system__）
    /// </summary>
    public string BaseUrl { get; set; } = "http://api.corp.local/__department__/__system__";

    /// <summary>
    /// 範例 2：微服務名稱（例如 "__department__-__system__-service"），由 Consul 服務發現解析實際健康節點位址
    /// </summary>
    public string? ServiceName { get; set; } = "__department__-__system__-service";

    /// <summary>
    /// 呼叫下游之逾時秒數。
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}

public class ConsulOptions
{
    /// <summary>
    /// Consul 叢集節點位址清單（支援多節點容錯）
    /// </summary>
    public List<string> Urls { get; set; } = new()
    {
        "http://tncimweb1.cminl.oa:8500/",
        "http://tncimweb2.cminl.oa:8500/"
    };

    /// <summary>
    /// 範例 1：Consul Key-Value 鍵值路徑（依據 ADR-014 規範）
    /// </summary>
    public string KvKey { get; set; } = "McpGateway/__department__/systems/__system__/Downstream/BaseUrl";
}
