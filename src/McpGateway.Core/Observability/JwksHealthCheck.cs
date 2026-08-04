using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using McpGateway.Core.Configuration;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace McpGateway.Core.Observability;

/// <summary>
/// Health check for JWKS endpoint availability.
/// Checks if JWKS endpoint is reachable or cached keys exist.
/// </summary>
public class JwksHealthCheck : IHealthCheck
{
    private readonly IMemoryCache _cache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<JwksHealthCheck> _logger;
    private readonly AuthOptions _authOptions;

    public JwksHealthCheck(
        IMemoryCache cache,
        IHttpClientFactory httpClientFactory,
        ILogger<JwksHealthCheck> logger,
        AuthOptions authOptions)
    {
        _cache = cache;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _authOptions = authOptions;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // If JWKS endpoint is not configured, skip this check
            if (string.IsNullOrEmpty(_authOptions.JwksEndpoint))
            {
                return HealthCheckResult.Healthy("JWKS endpoint not configured");
            }

            var jwksCacheKey = $"jwks_cache_{_authOptions.JwksEndpoint}";
            
            // Check if cached keys exist
            if (_cache.TryGetValue(jwksCacheKey, out _))
            {
                _logger.LogDebug("JWKS health check: cached keys exist");
                return HealthCheckResult.Healthy("JWKS cached keys available");
            }

            // Try to fetch JWKS endpoint with timeout
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(2); // 2 second timeout for health check
            
            using var response = await httpClient.GetAsync(_authOptions.JwksEndpoint, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("JWKS health check: endpoint reachable");
                return HealthCheckResult.Healthy("JWKS endpoint reachable");
            }

            return HealthCheckResult.Unhealthy($"JWKS endpoint returned status: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JWKS health check failed");
            return HealthCheckResult.Unhealthy($"JWKS health check failed: {ex.Message}");
        }
    }
}