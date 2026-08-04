using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace McpGateway.Core.Observability;

/// <summary>
/// Middleware that generates and tracks a unique correlation ID for each request.
/// The correlation ID is used to trace requests across services and in audit logs.
/// </summary>
public class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Processes the HTTP request, generating or using an existing correlation ID.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        // Generate a new correlation ID if one doesn't exist
        var correlationId = GetOrCreateCorrelationId(context);
        
        // Store in HttpContext.Items for access throughout the request
        context.Items["CorrelationId"] = correlationId;
        
        // Ensure the response includes the correlation ID header
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(CorrelationIdHeader))
            {
                context.Response.Headers.Append(CorrelationIdHeader, correlationId);
            }
            return Task.CompletedTask;
        });

        await _next(context);
    }

    private string GetOrCreateCorrelationId(HttpContext context)
    {
        // Check if correlation ID is provided in request header
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var existingId))
        {
            return existingId.ToString();
        }

        // Generate a new GUID-based correlation ID
        return Guid.NewGuid().ToString("N");
    }
}