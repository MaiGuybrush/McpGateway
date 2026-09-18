using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Consul;
using McpGateway.__Department__.__System__.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace McpGateway.__Department__.__System__.Services;

/// <summary>
/// 範例 2：downstream 使用 serviceName，透過 Consul 服務發現解析取得健康節點之 BaseUrl。
/// </summary>
public class ConsulServiceDiscoveryDownstreamResolver : IDownstreamUrlResolver
{
    private readonly __System__Options _options;
    private readonly ILogger<ConsulServiceDiscoveryDownstreamResolver> _logger;
    private readonly Func<string, IConsulClient> _consulClientFactory;

    public ConsulServiceDiscoveryDownstreamResolver(
        IOptions<__System__Options> options,
        ILogger<ConsulServiceDiscoveryDownstreamResolver> logger,
        Func<string, IConsulClient>? consulClientFactory = null)
    {
        _options = options.Value;
        _logger = logger;
        _consulClientFactory = consulClientFactory ?? (url => new ConsulClient(cfg => cfg.Address = new Uri(url)));
    }

    /// <inheritdoc />
    public async Task<string> ResolveBaseUrlAsync(CancellationToken cancellationToken = default)
    {
        var serviceName = _options.Downstream?.ServiceName ?? "__department__-__system__-service";
        var consulUrls = _options.Consul?.Urls;
        var fallbackUrl = _options.Downstream?.BaseUrl ?? "http://api.corp.local/__department__/__system__";

        if (consulUrls == null || consulUrls.Count == 0)
        {
            return fallbackUrl;
        }

        foreach (var url in consulUrls)
        {
            if (string.IsNullOrWhiteSpace(url)) continue;

            try
            {
                using var client = _consulClientFactory(url);
                // 查詢健康檢查通過之微服務執行個體
                var healthResult = await client.Health.Service(serviceName, tag: null, passingOnly: true, cancellationToken);

                if (healthResult.Response != null && healthResult.Response.Length > 0)
                {
                    // 取得首個健康節點
                    var entry = healthResult.Response[0];
                    var serviceAddress = !string.IsNullOrWhiteSpace(entry.Service.Address)
                        ? entry.Service.Address
                        : entry.Node.Address;
                    var port = entry.Service.Port;

                    var resolved = $"http://{serviceAddress}:{port}";
                    _logger.LogInformation("成功透過 Consul 服務發現 ({ServiceName}) 解析 BaseUrl: {BaseUrl}", serviceName, resolved);
                    return resolved;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "向 Consul 節點 {Url} 查詢服務 '{ServiceName}' 失敗，嘗試下一個節點...", url, serviceName);
            }
        }

        _logger.LogWarning("Consul 服務發現未找到健康執行個體，回退至預設 BaseUrl: {FallbackUrl}", fallbackUrl);
        return fallbackUrl;
    }
}
