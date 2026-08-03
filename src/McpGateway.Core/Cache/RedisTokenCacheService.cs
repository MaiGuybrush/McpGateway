using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace McpGateway.Core.Cache;

/// <summary>
/// Redis implementation of token cache service.
/// </summary>
public class RedisTokenCacheService : ITokenCacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisTokenCacheService> _logger;

    /// <summary>
    /// Initializes a new instance of the RedisTokenCacheService.
    /// </summary>
    /// <param name="redis">The Redis connection multiplexer.</param>
    /// <param name="logger">The logger.</param>
    public RedisTokenCacheService(IConnectionMultiplexer redis, ILogger<RedisTokenCacheService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ToolContext?> GetAsync(string tokenType, string token)
    {
        try
        {
            var key = GetCacheKey(tokenType, token);
            var db = _redis.GetDatabase();
            var cachedValue = await db.StringGetAsync(key);

            if (cachedValue.IsNullOrEmpty)
            {
                return null;
            }

            return JsonSerializer.Deserialize<ToolContext>(cachedValue!);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read from token cache");
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task SetAsync(string tokenType, string token, ToolContext context, int ttlMinutes)
    {
        try
        {
            var key = GetCacheKey(tokenType, token);
            var db = _redis.GetDatabase();
            var value = JsonSerializer.Serialize(context);
            var ttl = TimeSpan.FromMinutes(ttlMinutes);

            await db.StringSetAsync(key, value, ttl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write to token cache");
            // Swallow exception for degraded mode
        }
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(string tokenType, string token)
    {
        try
        {
            var key = GetCacheKey(tokenType, token);
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove from token cache");
        }
    }

    private string GetCacheKey(string tokenType, string token)
    {
        // Format: auth:{tokenType}:{SHA256(token)}
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
        var tokenHash = Convert.ToBase64String(hash).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        
        return $"auth:{tokenType}:{tokenHash}";
    }
}