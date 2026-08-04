using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using McpGateway.Core.Tools;

namespace McpGateway.Core.Auth;

/// <summary>
/// Authentication proxy for validating caller identity and forwarding identity headers.
/// Implements ADR-006 security model.
/// </summary>
public class AuthenticationProxy
{
    private readonly ILogger<AuthenticationProxy> _logger;

    public AuthenticationProxy(ILogger<AuthenticationProxy> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validates the incoming request authentication.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>True if authentication is valid.</returns>
    public async Task<bool> ValidateAsync(HttpContext context)
    {
        // API-KEY validation is handled by ApiKeyAuthenticationMiddleware
        // This method can be used for JWT validation or other auth methods
        _logger.LogDebug("Authentication validation called");
        return await Task.FromResult(true);
    }

    /// <summary>
    /// Extracts identity headers to forward downstream.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>Dictionary of identity headers.</returns>
    public Dictionary<string, string> ExtractIdentityHeaders(HttpContext context)
    {
        var headers = new Dictionary<string, string>();
        
        if (context.Items["ToolContext"] is ToolContext toolContext)
        {
            headers["X-User-Id"] = toolContext.UserId;
            headers["X-User-Department"] = toolContext.Department;
            headers["X-User-Role"] = toolContext.Role;
            headers["X-Auth-Type"] = toolContext.TokenType;
            headers["X-Correlation-Id"] = toolContext.CorrelationId;
        }
        
        return headers;
    }
    
    /// <summary>
    /// Gets the tool context from HTTP context if available.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>The tool context or null.</returns>
    public ToolContext? GetToolContext(HttpContext context)
    {
        return context.Items["ToolContext"] as ToolContext;
    }
}
