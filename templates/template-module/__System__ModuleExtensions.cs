using McpGateway.Core.Subsystems;
using McpGateway.__Department__.__System__.Configuration;
using McpGateway.__Department__.__System__.Services;
using McpGateway.__Department__.__System__.Tools.__ToolClass__;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace McpGateway.__Department__.__System__;

/// <summary>
/// 提供 __System__ 子系統模組向宿主註冊之擴充方法。
/// </summary>
public static class __System__ModuleExtensions
{
    /// <summary>
    /// 註冊 __System__ 子系統之組態、服務與 MCP 工具白名單。
    /// </summary>
    /// <param name="services">DI 服務集合。</param>
    /// <param name="configuration">應用程式組態。</param>
    /// <returns>DI 服務集合以利鏈式呼叫。</returns>
    public static IServiceCollection Add__System__Subsystem(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. 綁定子系統專屬組態
        services.Configure<__System__Options>(
            configuration.GetSection(__System__Options.SectionName));

        // 2. 註冊下游服務位址解析器（提供兩種實作範例供選擇）
        // 範例 1：從 Consul Key-Value 取得 downstream base url (預設啟用)
        services.AddSingleton<IDownstreamUrlResolver, ConsulKvDownstreamResolver>();

        // 範例 2：downstream 改為使用 serviceName，透過 Consul 服務發現解析健康節點之 base url (依需求切換)
        // services.AddSingleton<IDownstreamUrlResolver, ConsulServiceDiscoveryDownstreamResolver>();

        // 3. 註冊子系統業務服務與 Downstream API HttpClient
        services.AddHttpClient<I__ToolClass__Service, __ToolClass__Service>();

        // 4. 透過 Core Fluent API 向 MCP 伺服器與白名單註冊工具
        services.AddMcpSubsystem("__system__", subsystem =>
        {
            subsystem.WithTools<__ToolClass__Tool>();
        });

        return services;
    }
}
