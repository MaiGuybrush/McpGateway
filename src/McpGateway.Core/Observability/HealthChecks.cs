using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace McpGateway.Core.Observability;

/// <summary>
/// Composite health checks for gateway components for liveness probe.
/// Liveness probe always returns healthy unless the process is dead.
/// </summary>
public class GatewayHealthChecks : IHealthCheck
{
    private readonly ILogger<GatewayHealthChecks> _logger;

    public GatewayHealthChecks(ILogger<GatewayHealthChecks> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Performs health check on gateway components for liveness probe.
    /// Always returns healthy for liveness - process is alive.
    /// </summary>
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Liveness health check executed");
        
        // Liveness probe - always healthy unless process is dead
        return Task.FromResult(HealthCheckResult.Healthy("Gateway is alive"));
    }
}

/// <summary>
/// Readiness health check that combines Redis and JWKS checks.
/// </summary>
public class GatewayReadinessHealthCheck : IHealthCheck
{
    private readonly RedisHealthCheck _redisCheck;
    private readonly JwksHealthCheck _jwksCheck;
    private readonly ILogger<GatewayReadinessHealthCheck> _logger;

    public GatewayReadinessHealthCheck(
        RedisHealthCheck redisCheck,
        JwksHealthCheck jwksCheck,
        ILogger<GatewayReadinessHealthCheck> logger)
    {
        _redisCheck = redisCheck;
        _jwksCheck = jwksCheck;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var checks = new List<(string name, HealthCheckResult result)>(2);

            // Check Redis if configured
            var redisResult = await _redisCheck.CheckHealthAsync(context, cancellationToken);
            checks.Add(("Redis", redisResult));

            // Check JWKS if endpoint configured
            var jwksResult = await _jwksCheck.CheckHealthAsync(context, cancellationToken);
            checks.Add(("JWKS", jwksResult));

            // Check if any check failed
            var failedChecks = checks.Where(c => c.result.Status == HealthStatus.Unhealthy).ToList();

            if (failedChecks.Any())
            {
                var data = new Dictionary<string, object>
                {
                    ["checks"] = checks.ToDictionary(
                        c => c.name,
                        c => new { status = c.result.Status.ToString(), description = c.result.Description }
                    )
                };

                return HealthCheckResult.Unhealthy(
                    $"Readiness check failed: {string.Join(", ", failedChecks.Select(f => f.name))}",
                    null,
                    data
                );
            }

            return HealthCheckResult.Healthy("Gateway is ready", new Dictionary<string, object>
            {
                ["checks"] = checks.ToDictionary(
                    c => c.name,
                    c => new { status = c.result.Status.ToString(), description = c.result.Description }
                )
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Readiness health check failed");
            return HealthCheckResult.Unhealthy($"Readiness check failed: {ex.Message}");
        }
    }
}
