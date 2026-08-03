using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace McpGateway.Core.Cache;

/// <summary>
/// Null implementation of token cache service (degraded mode when Redis is unavailable).
/// </summary>
public class NullTokenCacheService : ITokenCacheService
{
    private readonly ILogger<NullTokenCacheService> _logger;

    public NullTokenCacheService(ILogger<NullTokenCacheService> logger)
    {
        _logger = logger;
        _logger.LogWarning("Token cache is disabled. Redis connection string not configured or connection failed. Auth performance may be degraded.");
    }

    public Task<ToolContext?> GetAsync(string tokenType, string token)
    {
        // Always cache miss
        return Task.FromResult<ToolContext?>(null);
    }

    public Task SetAsync(string tokenType, string token, ToolContext context, int ttlMinutes)
    {
        // No-op for null cache
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string tokenType, string token)
    {
        // No-op for null cache
        return Task.CompletedTask;
    }
}