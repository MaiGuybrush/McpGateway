using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using McpGateway.Core.Configuration;

namespace McpGateway.Core.Auth;

/// <summary>
/// Resolves UAC API endpoints dynamically from Consul K/V with multi-node failover and local fallback support.
/// </summary>
public class UacApiEndpointResolver : IUacApiEndpointResolver
{
    private readonly HttpClient _httpClient;
    private readonly McpGatewayOptions _gatewayOptions;
    private readonly AuthOptions _authOptions;
    private readonly ILogger<UacApiEndpointResolver> _logger;

    private readonly SemaphoreSlim _lock = new(1, 1);
    private IReadOnlyList<string>? _cachedEndpoints;
    private DateTime _lastResolvedUtc = DateTime.MinValue;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

    public UacApiEndpointResolver(
        HttpClient httpClient,
        IOptions<McpGatewayOptions> options,
        ILogger<UacApiEndpointResolver> logger)
    {
        _httpClient = httpClient;
        _gatewayOptions = options.Value;
        _authOptions = options.Value.Auth ?? new AuthOptions();
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ResolveEndpointsAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedEndpoints != null && (DateTime.UtcNow - _lastResolvedUtc) < _cacheDuration)
        {
            return _cachedEndpoints;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedEndpoints != null && (DateTime.UtcNow - _lastResolvedUtc) < _cacheDuration)
            {
                return _cachedEndpoints;
            }

            var endpoints = await LoadEndpointsFromConsulOrFallbackAsync(cancellationToken);
            _cachedEndpoints = endpoints;
            _lastResolvedUtc = DateTime.UtcNow;
            return _cachedEndpoints;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<IReadOnlyList<string>> LoadEndpointsFromConsulOrFallbackAsync(CancellationToken cancellationToken)
    {
        var consulUrls = (_authOptions.ConsulUrls.Count > 0
            ? _authOptions.ConsulUrls
            : _gatewayOptions.Consul?.Urls ?? new List<string>())
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .ToList();

        var consulKey = string.IsNullOrWhiteSpace(_authOptions.ConsulKey)
            ? "ApiUrls.ProductionOa"
            : _authOptions.ConsulKey;

        foreach (var consulUrl in consulUrls)
        {
            try
            {
                var trimmedBase = consulUrl.TrimEnd('/');
                var requestUrl = $"{trimmedBase}/v1/kv/{consulKey}?raw";
                _logger.LogInformation("Attempting to resolve UAC API endpoints from Consul node {Url} (Key: {Key})", trimmedBase, consulKey);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(3));

                var response = await _httpClient.GetAsync(requestUrl, cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(cts.Token);
                    var endpoints = ParseUacEndpoints(content);
                    if (endpoints.Count > 0)
                    {
                        _logger.LogInformation("Successfully resolved {Count} UAC API endpoint(s) from Consul node {Url}", endpoints.Count, trimmedBase);
                        return endpoints;
                    }
                }
                else
                {
                    _logger.LogWarning("Consul node {Url} returned status code {StatusCode} for key {Key}", trimmedBase, response.StatusCode, consulKey);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Failed to resolve UAC API endpoints from Consul node {Url}, trying next node...", consulUrl);
            }
        }

        // Fallback
        if (_authOptions.FallbackUacApiUrls.Count > 0)
        {
            _logger.LogWarning("Consul resolution failed or not configured; using configured FallbackUacApiUrls ({Count} endpoints)", _authOptions.FallbackUacApiUrls.Count);
            return _authOptions.FallbackUacApiUrls.Where(u => !string.IsNullOrWhiteSpace(u)).ToList().AsReadOnly();
        }

        if (!string.IsNullOrWhiteSpace(_authOptions.ApiKeyServiceUrl))
        {
            _logger.LogWarning("Using legacy ApiKeyServiceUrl fallback: {Url}", _authOptions.ApiKeyServiceUrl);
            return new List<string> { _authOptions.ApiKeyServiceUrl }.AsReadOnly();
        }

        _logger.LogWarning("No UAC API endpoints could be resolved and no fallback URLs configured");
        return Array.Empty<string>();
    }

    private IReadOnlyList<string> ParseUacEndpoints(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // If Consul returned standard KV JSON array [{ "Value": "<base64>" }]
            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            {
                var first = root[0];
                if (first.TryGetProperty("Value", out var valProp) && valProp.ValueKind == JsonValueKind.String)
                {
                    var base64 = valProp.GetString();
                    if (!string.IsNullOrEmpty(base64))
                    {
                        var rawJson = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
                        return ParseUacEndpoints(rawJson);
                    }
                }
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in root.EnumerateObject())
                {
                    if (string.Equals(prop.Name, "UacApi", StringComparison.OrdinalIgnoreCase) && prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        var list = new List<string>();
                        foreach (var item in prop.Value.EnumerateArray())
                        {
                            var s = item.GetString();
                            if (!string.IsNullOrWhiteSpace(s))
                            {
                                list.Add(s);
                            }
                        }
                        return list.AsReadOnly();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse JSON for UAC API endpoints from Consul response");
        }

        return Array.Empty<string>();
    }
}
