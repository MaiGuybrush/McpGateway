namespace McpGateway.Core.Observability;

/// <summary>
/// Service for accessing the current request's correlation ID.
/// </summary>
public interface ICorrelationIdService
{
    /// <summary>
    /// Gets the correlation ID for the current request.
    /// </summary>
    string GetCorrelationId();
}