using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace McpGateway.Core.Observability;

/// <summary>
/// Health checks for gateway components.
/// </summary>
public class GatewayHealthChecks : IHealthCheck
{
    private readonly ILogger<GatewayHealthChecks> _logger;

    public GatewayHealthChecks(ILogger<GatewayHealthChecks> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Performs health check on gateway components.
    /// </summary>
    /// <param name="context">Health check context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Health check result.</returns>
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Health check executed");
        
        // For minimal version, always return healthy
        // TODO: Implement actual health checks in future sprints
        // - Downstream service connectivity
        // - Authentication service availability
        // - Tool registry integrity
        
        return Task.FromResult(HealthCheckResult.Healthy("Gateway is healthy"));
    }
}
