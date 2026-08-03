using System;
using System.Threading.Tasks;
using McpGateway.Core.Tools;

namespace McpGateway.Core.Cache;

/// <summary>
/// Service for caching token validation results.
/// </summary>
public interface ITokenCacheService
{
    /// <summary>
    /// Gets the cached token validation result.
    /// </summary>
    /// <param name="tokenType">The token type (e.g., "JWT", "API-KEY").</param>
    /// <param name="token">The token value.</param>
    /// <returns>The cached tool context if found, null otherwise.</returns>
    Task<ToolContext?> GetAsync(string tokenType, string token);

    /// <summary>
    /// Sets the cached token validation result.
    /// </summary>
    /// <param name="tokenType">The token type.</param>
    /// <param name="token">The token value.</param>
    /// <param name="context">The tool context to cache.</param>
    /// <param name="ttlMinutes">The TTL in minutes.</param>
    /// <returns>Task.</returns>
    Task SetAsync(string tokenType, string token, ToolContext context, int ttlMinutes);

    /// <summary>
    /// Removes a cached token validation result.
    /// </summary>
    /// <param name="tokenType">The token type.</param>
    /// <param name="token">The token value.</param>
    /// <returns>Task.</returns>
    Task RemoveAsync(string tokenType, string token);
}