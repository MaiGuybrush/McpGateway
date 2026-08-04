using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using McpGateway.Core.Validation;

namespace McpGateway.Core.Auth;

/// <summary>
/// Middleware for handling API-KEY authentication.
/// Extracts API-KEY from Authorization header and validates it.
/// </summary>
public class ApiKeyAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger;
    private readonly IApiKeyValidator _apiKeyValidator;

    public ApiKeyAuthenticationMiddleware(
        RequestDelegate next,
        ILogger<ApiKeyAuthenticationMiddleware> logger,
        IApiKeyValidator apiKeyValidator)
    {
        _next = next;
        _logger = logger;
        _apiKeyValidator = apiKeyValidator;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Check if Authorization header exists and starts with "Bearer "
        var authorizationHeader = context.Request.Headers["Authorization"].ToString();
        
        if (string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Bearer "))
        {
            // Skip API-KEY validation, let other auth methods handle it
            await _next(context);
            return;
        }

        var apiKey = authorizationHeader["Bearer ".Length..].Trim();
        
        if (string.IsNullOrEmpty(apiKey))
        {
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            await context.Response.WriteAsync("Missing API-KEY");
            return;
        }

        try
        {
            // Generate correlation ID for tracking
            var correlationId = Guid.NewGuid().ToString();
            
            // Validate API-KEY
            var toolContext = await _apiKeyValidator.ValidateAsync(apiKey, correlationId);
            
            if (toolContext == null)
            {
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                await context.Response.WriteAsync("Invalid API-KEY");
                return;
            }

            // Store tool context in HttpContext for downstream use
            context.Items["ToolContext"] = toolContext;
            
            _logger.LogInformation("API-KEY authentication successful for user {UserId}", toolContext.UserId);
            
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during API-KEY validation");
            context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
            await context.Response.WriteAsync("API-KEY validation service error");
        }
    }
}