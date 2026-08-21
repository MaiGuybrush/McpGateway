using System.ComponentModel.DataAnnotations;

namespace McpGateway.Core.Configuration;

/// <summary>
/// Configuration options for MCP Gateway.
/// </summary>
public class McpGatewayOptions
{
    /// <summary>
    /// Gets or sets the department name (required).
    /// </summary>
    [Required(ErrorMessage = "Department is required")]
    public string Department { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the route prefix for MCP endpoints (required).
    /// </summary>
    [Required(ErrorMessage = "RoutePrefix is required")]
    public string RoutePrefix { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets whether to enable health check endpoints.
    /// </summary>
    public bool EnableHealthChecks { get; set; } = true;
    
    /// <summary>
    /// Gets or sets Ocelot configuration options.
    /// </summary>
    public OcelotOptions? Ocelot { get; set; }
    
    /// <summary>
    /// Gets or sets authentication configuration options.
    /// </summary>
    public AuthOptions? Auth { get; set; }
    
    /// <summary>
    /// Gets or sets token cache configuration options.
    /// </summary>
    public TokenCacheOptions? TokenCache { get; set; }
    
    /// <summary>
    /// Gets or sets audit configuration options.
    /// </summary>
    public AuditOptions? Audit { get; set; }

    /// <summary>
    /// Gets or sets Consul configuration options.
    /// </summary>
    public ConsulOptions? Consul { get; set; }
}

/// <summary>
/// Consul configuration options.
/// </summary>
public class ConsulOptions
{
    /// <summary>
    /// Gets or sets the Consul cluster URLs.
    /// </summary>
    public List<string> Urls { get; set; } = new();
}

/// <summary>
/// Ocelot configuration options.
/// </summary>
public class OcelotOptions
{
    /// <summary>
    /// Gets or sets the base URL for Ocelot gateway.
    /// </summary>
    public string? BaseUrl { get; set; }
    
    /// <summary>
    /// Gets or sets the timeout in seconds for downstream calls (default: 10).
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;
    
    /// <summary>
    /// Gets or sets the retry configuration.
    /// </summary>
    public RetryOptions? Retry { get; set; }
}

/// <summary>
/// Retry configuration options for downstream calls.
/// </summary>
public class RetryOptions
{
    /// <summary>
    /// Gets or sets the number of retry attempts (default: 2).
    /// </summary>
    public int Count { get; set; } = 2;
    
    /// <summary>
    /// Gets or sets the backoff delay in milliseconds between retries (default: 200).
    /// </summary>
    public int BackoffMs { get; set; } = 200;
}

/// <summary>
/// Authentication configuration options.
/// </summary>
public class AuthOptions
{
    /// <summary>
    /// Gets or sets the authentication provider.
    /// </summary>
    public string? Provider { get; set; } = "API-KEY";
    
    /// <summary>
    /// Gets or sets whether authentication is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the registered system name in UAC API.
    /// </summary>
    public string? SystemName { get; set; }

    /// <summary>
    /// Gets or sets the Consul K/V key for API URLs.
    /// </summary>
    public string ConsulKey { get; set; } = "ApiUrls.ProductionOa";

    /// <summary>
    /// Gets or sets the Consul URLs for dynamic endpoint discovery.
    /// </summary>
    public List<string> ConsulUrls { get; set; } = new();

    /// <summary>
    /// Gets or sets fallback UAC API URLs if Consul discovery is unavailable.
    /// </summary>
    public List<string> FallbackUacApiUrls { get; set; } = new();

    /// <summary>
    /// Gets or sets the API-KEY token cache TTL in minutes (default: 30).
    /// </summary>
    public int CacheTtlMinutes { get; set; } = 30;
    
    /// <summary>
    /// Gets or sets the JWKS endpoint URL.
    /// </summary>
    public string? JwksEndpoint { get; set; }
    
    /// <summary>
    /// Gets or sets the JWKS cache TTL in hours (default: 24).
    /// </summary>
    public int JwksCacheHours { get; set; } = 24;
    
    /// <summary>
    /// Gets or sets the API-KEY validation service URL.
    /// </summary>
    public string? ApiKeyServiceUrl { get; set; }
    
    /// <summary>
    /// Gets or sets the API-KEY validation timeout in seconds (default: 3).
    /// </summary>
    public int ApiKeyTimeoutSeconds { get; set; } = 3;
}

/// <summary>
/// Token cache configuration options.
/// </summary>
public class TokenCacheOptions
{
    /// <summary>
    /// Gets or sets the cache type (e.g., "Memory", "Redis").
    /// </summary>
    public string? Type { get; set; }
    
    /// <summary>
    /// Gets or sets the Redis connection string.
    /// </summary>
    public string? ConnectionString { get; set; }
    
    /// <summary>
    /// Gets or sets the JWT expiry skew in minutes (default: 1).
    /// </summary>
    public int JwtExpirySkewMinutes { get; set; } = 1;
    
    /// <summary>
    /// Gets or sets the API key TTL in minutes (default: 5).
    /// </summary>
    public int ApiKeyTtlMinutes { get; set; } = 5;
}

/// <summary>
/// Audit configuration options.
/// </summary>
public class AuditOptions
{
    /// <summary>
    /// Gets or sets whether audit logging is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// Gets or sets the audit log storage path.
    /// </summary>
    public string? LogPath { get; set; }
}