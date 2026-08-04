using System.Threading.Tasks;
using McpGateway.Core.Tools;

namespace McpGateway.Core.Validation;

/// <summary>
/// Validates API-KEY tokens against the API-KEY validation service with degradation support.
/// </summary>
public interface IApiKeyValidator
{
    /// <summary>
    /// Validates an API-KEY token and returns the tool context.
    /// </summary>
    /// <param name="apiKey">The API-KEY token to validate.</param>
    /// <param name="correlationId">The correlation ID for logging degradation scenarios.</param>
    /// <returns>The tool context if validation succeeds, null otherwise.</returns>
    Task<ToolContext?> ValidateAsync(string apiKey, string correlationId);
}