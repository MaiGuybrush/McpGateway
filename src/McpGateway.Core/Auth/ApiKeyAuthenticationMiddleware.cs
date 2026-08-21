using System;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using McpGateway.Core.Configuration;
using McpGateway.Core.Validation;

namespace McpGateway.Core.Auth;

/// <summary>
/// Middleware for handling enterprise UAC API-KEY authentication.
/// Extracts API-KEY from X-Api-Key or Authorization Bearer header and validates it.
/// </summary>
public class ApiKeyAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger;
    private readonly IApiKeyValidator _apiKeyValidator;
    private readonly AuthOptions _authOptions;

    public ApiKeyAuthenticationMiddleware(
        RequestDelegate next,
        ILogger<ApiKeyAuthenticationMiddleware> logger,
        IApiKeyValidator apiKeyValidator,
        IOptions<McpGatewayOptions> options)
    {
        _next = next;
        _logger = logger;
        _apiKeyValidator = apiKeyValidator;
        _authOptions = options.Value.Auth ?? new AuthOptions();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // If authentication is disabled, pass through directly
        if (!_authOptions.Enabled)
        {
            await _next(context);
            return;
        }

        // Bypass health check endpoints
        var path = context.Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // 1. Extract API Key: Priority 1: X-Api-Key, Priority 2: Authorization: Bearer <key>
        string? apiKey = null;

        var xApiKey = context.Request.Headers["X-Api-Key"].ToString();
        if (!string.IsNullOrWhiteSpace(xApiKey))
        {
            apiKey = xApiKey.Trim();
        }
        else
        {
            var authHeader = context.Request.Headers["Authorization"].ToString();
            if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                apiKey = authHeader["Bearer ".Length..].Trim();
            }
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Rejecting request: Missing API-KEY in X-Api-Key and Authorization header");
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\": \"Missing API-KEY\"}");
            return;
        }

        try
        {
            var correlationId = context.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString();

            // 2. Validate API Key
            var toolContext = await _apiKeyValidator.ValidateAsync(apiKey, correlationId);
            if (toolContext == null)
            {
                _logger.LogWarning("Rejecting request: Invalid or unauthorized API-KEY");
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"error\": \"Invalid API-KEY\"}");
                return;
            }

            // 3. Store tool context in HttpContext for downstream tools and audit logger
            context.Items["ToolContext"] = toolContext;

            // 4. Inject ClaimsPrincipal
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, toolContext.UserId),
                new Claim(ClaimTypes.Name, toolContext.Role),
                new Claim("Department", toolContext.Department ?? string.Empty)
            };
            var identity = new ClaimsIdentity(claims, "ApiKey");
            context.User = new ClaimsPrincipal(identity);

            _logger.LogInformation("API-KEY authentication successful for user {UserId} ({UserName})", toolContext.UserId, toolContext.Role);

            await _next(context);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "API-KEY validation service unavailable or encountered network failure");
            context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\": \"API-KEY validation service error\"}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during API-KEY authentication");
            context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\": \"API-KEY validation service error\"}");
        }
    }
}