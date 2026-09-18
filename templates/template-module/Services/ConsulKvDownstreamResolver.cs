using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Consul;
using McpGateway.__Department__.__System__.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace McpGateway.__Department__.__System__.Services;

/// <summary>
/// 範例 1：從 Consul Key-Value 取得 downstream base url（支援多節點容錯與本地 Fallback）。
/// </summary>
public class ConsulKvDownstreamResolver : IDownstreamUrlResolver
{
    private readonly __System__Options _options;
    private readonly ILogger<ConsulKvDownstreamResolver> _logger;
    private readonly Func<string, IConsulClient> _consulClientFactory;

    public ConsulKvDownstreamResolver(
        IOptions<__System__Options> options,
        ILogger<ConsulKvDownstreamResolver> logger,
        Func<string, IConsulClient>? consulClientFactory = null)
    {
        _options = options.Value;
        _logger = logger;
        _consulClientFactory = consulClientFactory ?? (url => new ConsulClient(cfg => cfg.Address = new Uri(url)));
    }

    /// <inheritdoc />
    public async Task<string> ResolveBaseUrlAsync(CancellationToken cancellationToken = default)
    {
        var consulUrls = _options.Consul?.Urls;
        var kvKey = _options.Consul?.KvKey ?? "McpGateway/__department__/systems/__system__/Downstream/BaseUrl";
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
                var getResult = await client.KV.Get(kvKey, cancellationToken);

                if (getResult.Response?.Value != null && getResult.Response.Value.Length > 0)
                {
                    var resolved = Encoding.UTF8.GetString(getResult.Response.Value).Trim();
                    if (!string.IsNullOrEmpty(resolved))
                    {
                        _logger.LogInformation("成功自 Consul KV ({Url}/{Key}) 解析 Downstream BaseUrl: {BaseUrl}", url, kvKey, resolved);
                        return resolved;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "向 Consul 節點 {Url} 讀取 Key '{Key}' 失敗，嘗試下一個節點...", url, kvKey);
            }
        }

        _logger.LogWarning("Consul KV 均無法解析或不可達，回退至預設 BaseUrl: {FallbackUrl}", fallbackUrl);
        return fallbackUrl;
    }
}
