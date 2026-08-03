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
}

/// <summary>
/// Ocelot configuration options.
/// </summary>
public class OcelotOptions
{
    /// <summary>
    /// Gets or sets the configuration file path.
    /// </summary>
    public string? ConfigFile { get; set; }
}

/// <summary>
/// Authentication configuration options.
/// </summary>
public class AuthOptions
{
    /// <summary>
    /// Gets or sets the authentication provider.
    /// </summary>
    public string? Provider { get; set; }
    
    /// <summary>
    /// Gets or sets whether authentication is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// Gets or sets the JWKS endpoint URL.
    /// </summary>
    public string? JwksEndpoint { get; set; }
    
    /// <summary>
    /// Gets or sets the JWKS cache TTL in hours (default: 24).
    /// </summary>
    public int JwksCacheHours { get; set; } = 24;
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