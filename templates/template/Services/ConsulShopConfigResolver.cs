using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Consul;
using McpGateway.__Department__.Configuration;
using McpGateway.__Department__.Exceptions;
using McpGateway.__Department__.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace McpGateway.__Department__.Services;

/// <summary>
/// 透過 Consul K/V 讀取 'ShopList'，支援雙節點容錯與本地 Fallback 的廠別解析器
/// </summary>
public class ConsulShopConfigResolver : IShopConfigResolver
{
    private readonly __OptionsClass__ _options;
    private readonly ILogger<ConsulShopConfigResolver> _logger;
    private readonly Func<string, IConsulClient> _consulClientFactory;

    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<ConsulShopItem>? _cachedShops;
    private DateTime _lastLoadedUtc = DateTime.MinValue;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(30);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ConsulShopConfigResolver(
        IOptions<__OptionsClass__> options,
        ILogger<ConsulShopConfigResolver> logger,
        Func<string, IConsulClient>? consulClientFactory = null)
    {
        _options = options.Value;
        _logger = logger;
        _consulClientFactory = consulClientFactory ?? (url => new ConsulClient(cfg => cfg.Address = new Uri(url)));
    }

    /// <inheritdoc />
    public async Task<ResolvedShopConfig> ResolveShopAsync(string shop, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(shop))
        {
            throw new ShopNotFoundException(shop, "廠別名稱不可為空。");
        }

        var shops = await GetOrLoadShopsAsync(cancellationToken);
        var trimmedShop = shop.Trim();

        // 1. 比對 Id (忽略大小寫，例如 "CF3", "cf3", "TFT7")
        var match = shops.FirstOrDefault(s => string.Equals(s.Id, trimmedShop, StringComparison.OrdinalIgnoreCase));
        if (match != null)
        {
            return ToResolvedConfig(match);
        }

        // 2. 比對 DllShopName (忽略大小寫，例如 "Cf3", "Tft7")
        match = shops.FirstOrDefault(s => string.Equals(s.DllShopName, trimmedShop, StringComparison.OrdinalIgnoreCase));
        if (match != null)
        {
            return ToResolvedConfig(match);
        }

        // 3. 通用別名解析 (例如 ARY7 -> TFT7, CEL7 -> LCD7)
        var aliasShop = ResolveAlias(trimmedShop);
        if (!string.Equals(aliasShop, trimmedShop, StringComparison.OrdinalIgnoreCase))
        {
            match = shops.FirstOrDefault(s => string.Equals(s.Id, aliasShop, StringComparison.OrdinalIgnoreCase)
                                           || string.Equals(s.DllShopName, aliasShop, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                return ToResolvedConfig(match);
            }
        }

        _logger.LogWarning("查無廠別設定: '{Shop}' (嘗試別名: '{Alias}')", shop, aliasShop);
        throw new ShopNotFoundException(shop);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConsulShopItem>> GetAllShopsAsync(CancellationToken cancellationToken = default)
    {
        var shops = await GetOrLoadShopsAsync(cancellationToken);
        return shops.AsReadOnly();
    }

    private async Task<List<ConsulShopItem>> GetOrLoadShopsAsync(CancellationToken cancellationToken)
    {
        if (_cachedShops != null && (DateTime.UtcNow - _lastLoadedUtc) < _cacheDuration)
        {
            return _cachedShops;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedShops != null && (DateTime.UtcNow - _lastLoadedUtc) < _cacheDuration)
            {
                return _cachedShops;
            }

            _cachedShops = await LoadShopsFromConsulOrFallbackAsync(cancellationToken);
            _lastLoadedUtc = DateTime.UtcNow;
            return _cachedShops;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<ConsulShopItem>> LoadShopsFromConsulOrFallbackAsync(CancellationToken cancellationToken)
    {
        var consulUrls = _options.ConsulUrls ?? new List<string>();
        var consulKey = string.IsNullOrWhiteSpace(_options.ConsulKey) ? "ShopList" : _options.ConsulKey;

        // 依序嘗試各 Consul 叢集節點 (雙節點容錯)
        foreach (var url in consulUrls)
        {
            if (string.IsNullOrWhiteSpace(url)) continue;

            try
            {
                _logger.LogInformation("正在從 Consul 節點 {Url} 讀取 Key: '{Key}'...", url, consulKey);
                using var client = _consulClientFactory(url);
                var queryResult = await client.KV.Get(consulKey, cancellationToken);

                if (queryResult.Response?.Value != null && queryResult.Response.Value.Length > 0)
                {
                    var json = Encoding.UTF8.GetString(queryResult.Response.Value);
                    var list = JsonSerializer.Deserialize<List<ConsulShopItem>>(json, JsonOptions);

                    if (list != null && list.Count > 0)
                    {
                        _logger.LogInformation("成功從 Consul 節點 {Url} 載入 {Count} 筆廠別設定", url, list.Count);
                        return list;
                    }
                }

                _logger.LogWarning("Consul 節點 {Url} 回傳的 Key '{Key}' 為空或無法解析", url, consulKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "連線至 Consul 節點 {Url} 讀取 Key '{Key}' 失敗，嘗試下一個節點...", url, consulKey);
            }
        }

        // 所有 Consul 節點均不可達時，回退至本地 Fallback 設定
        _logger.LogWarning("所有 Consul 節點均無法連線，啟用本地 Fallback 廠別設定 (共 {Count} 筆)", _options.FallbackShops?.Count ?? 0);
        var fallbackList = new List<ConsulShopItem>();
        if (_options.FallbackShops != null)
        {
            foreach (var kvp in _options.FallbackShops)
            {
                fallbackList.Add(new ConsulShopItem
                {
                    Id = kvp.Key,
                    Fab = kvp.Key,
                    Category = "__department__",
                    DllShopName = kvp.Key,
                    EnvironmentType = "production"
                });
            }
        }
        return fallbackList;
    }

    private static string ResolveAlias(string shop)
    {
        var upper = shop.ToUpperInvariant();
        if (upper.StartsWith("ARY"))
        {
            return "TFT" + upper[3..];
        }
        if (upper.StartsWith("CEL"))
        {
            return "LCD" + upper[3..];
        }
        return shop;
    }

    private static ResolvedShopConfig ToResolvedConfig(ConsulShopItem item)
    {
        var wipCode = item.Id;
        return new ResolvedShopConfig(
            ShopId: item.Id,
            Fab: item.Fab,
            WipCode: wipCode,
            Category: item.Category,
            DllShopName: item.DllShopName,
            EnvironmentType: item.EnvironmentType
        );
    }
}
