using Microsoft.AspNetCore.Http;

namespace McpGateway.Core.Observability;

/// <summary>
/// Scoped service for accessing correlation ID from HttpContext.
/// </summary>
public class CorrelationIdService : ICorrelationIdService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CorrelationIdService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetCorrelationId()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null)
        {
            return string.Empty;
        }

        return context.Items["CorrelationId"] as string ?? string.Empty;
    }
}