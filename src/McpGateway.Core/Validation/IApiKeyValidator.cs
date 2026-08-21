using System.Threading;
using System.Threading.Tasks;
using McpGateway.Core.Tools;

namespace McpGateway.Core.Validation;

/// <summary>
/// Validates API-KEY tokens against the UAC API validation service with degradation and multi-node failover support.
/// </summary>
public interface IApiKeyValidator
{
    /// <summary>
    /// Validates an API-KEY token and returns the tool context.
    /// </summary>
    /// <param name="apiKey">The API-KEY token to validate.</param>
    /// <param name="correlationId">The correlation ID for logging degradation scenarios.</param>
    /// <returns>The tool context if validation succeeds, null if unauthorized.</returns>
    Task<ToolContext?> ValidateAsync(string apiKey, string correlationId);

    /// <summary>
    /// Validates that the configured system name is registered in UAC API.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the system is registered; otherwise false.</returns>
    Task<bool> ValidateSystemRegisteredAsync(CancellationToken cancellationToken = default);
}