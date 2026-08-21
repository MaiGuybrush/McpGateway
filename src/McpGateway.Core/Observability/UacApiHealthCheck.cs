using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using McpGateway.Core.Auth;
using McpGateway.Core.Configuration;

namespace McpGateway.Core.Observability;

/// <summary>
/// Health check for enterprise UAC API readiness.
/// </summary>
public class UacApiHealthCheck : IHealthCheck
{
    private readonly IUacApiEndpointResolver _endpointResolver;
    private readonly HttpClient _httpClient;
    private readonly AuthOptions _authOptions;
    private readonly ILogger<UacApiHealthCheck> _logger;

    public UacApiHealthCheck(
        IUacApiEndpointResolver endpointResolver,
        HttpClient httpClient,
        IOptions<McpGatewayOptions> options,
        ILogger<UacApiHealthCheck> logger)
    {
        _endpointResolver = endpointResolver;
        _httpClient = httpClient;
        _authOptions = options.Value.Auth ?? new AuthOptions();
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_authOptions.Enabled)
        {
            return HealthCheckResult.Healthy("UAC Authentication is disabled");
        }

        try
        {
            var endpoints = await _endpointResolver.ResolveEndpointsAsync(cancellationToken);
            if (endpoints == null || endpoints.Count == 0)
            {
                return HealthCheckResult.Unhealthy("No UAC API endpoints available");
            }

            foreach (var endpoint in endpoints)
            {
                var baseUrl = endpoint.TrimEnd('/');
                var requestUrl = $"{baseUrl}/api-keys/systems";

                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(TimeSpan.FromSeconds(2));

                    var response = await _httpClient.GetAsync(requestUrl, cts.Token);
                    if (response.IsSuccessStatusCode)
                    {
                        return HealthCheckResult.Healthy($"UAC API reachable at {baseUrl}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Health check probe to {Endpoint} failed, testing next node...", baseUrl);
                }
            }

            return HealthCheckResult.Unhealthy("All UAC API endpoints failed health check probe");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "UAC API health check error");
            return HealthCheckResult.Unhealthy($"UAC API health check failed: {ex.Message}");
        }
    }
}
