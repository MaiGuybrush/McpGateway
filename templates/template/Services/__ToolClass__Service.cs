using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using McpGateway.__Department__.Configuration;
using McpGateway.__Department__.Tools.__ToolClass__;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace McpGateway.__Department__.Services;

/// <summary>
/// __ToolClass__ 服務實作，包含 HTTP 呼叫與 IMemoryCache 快取
/// </summary>
public class __ToolClass__Service : I__ToolClass__Service
{
    private readonly HttpClient _httpClient;
    private readonly IShopConfigResolver _shopResolver;
    private readonly IMemoryCache _cache;
    private readonly __OptionsClass__ _options;
    private readonly ILogger<__ToolClass__Service> _logger;

    public __ToolClass__Service(
        HttpClient httpClient,
        IShopConfigResolver shopResolver,
        IMemoryCache cache,
        IOptions<__OptionsClass__> options,
        ILogger<__ToolClass__Service> logger)
    {
        _httpClient = httpClient;
        _shopResolver = shopResolver;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<__ToolClass__ResultDto> ExecuteAsync(__ToolClass__Input input, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"__department__:{input.Shop}:{input.QueryId}";
        if (_cache.TryGetValue<__ToolClass__ResultDto>(cacheKey, out var cachedResult) && cachedResult != null)
        {
            _logger.LogInformation("從快取取得 __ToolClass__ 結果: {CacheKey}", cacheKey);
            return cachedResult;
        }

        var shopConfig = await _shopResolver.ResolveShopAsync(input.Shop, cancellationToken);
        _logger.LogInformation("已解析廠別 {Shop} -> {Fab}", input.Shop, shopConfig.Fab);

        // TODO: 依據業務需求調整下游 API 呼叫邏輯
        var result = new __ToolClass__ResultDto(
            QueryId: input.QueryId,
            Shop: shopConfig.ShopId,
            Status: "Active",
            Message: $"Successfully queried {input.QueryId} from {shopConfig.ShopId}"
        );

        var cacheDuration = TimeSpan.FromMinutes(_options.CacheMinutes > 0 ? _options.CacheMinutes : 3);
        _cache.Set(cacheKey, result, cacheDuration);

        return result;
    }
}
