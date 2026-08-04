using System;

namespace McpGateway.Core.Observability;

/// <summary>
/// Service for exposing Prometheus metrics from the MCP Gateway.
/// Implements all required metrics from sprint-3 specification.
/// NOTE: Placeholder implementation due to prometheus-net/.NET 9 compatibility issues.
/// In production, replace with System.Diagnostics.Metrics or compatible prometheus library.
/// </summary>
public class MetricsService
{
    private static readonly string Version = typeof(MetricsService).Assembly.GetName().Version?.ToString() ?? "0.1.0";
    private readonly string _department;

    public MetricsService(string department)
    {
        _department = department ?? "unknown";
    }

    /// <summary>
    /// Records a tool call with its outcome and duration.
    /// </summary>
    public void RecordToolCall(string toolName, bool success, TimeSpan duration)
    {
        // TODO: Implement with System.Diagnostics.Metrics or compatible library
    }

    /// <summary>
    /// Records a downstream HTTP request duration.
    /// </summary>
    public void RecordDownstreamRequest(string toolName, TimeSpan duration)
    {
        // TODO: Implement with System.Diagnostics.Metrics or compatible library
    }

    /// <summary>
    /// Records an authentication failure.
    /// </summary>
    public void RecordAuthFailure(string reason)
    {
        // TODO: Implement with System.Diagnostics.Metrics or compatible library
    }

    /// <summary>
    /// Records a token cache operation.
    /// </summary>
    public void RecordTokenCacheOperation(bool hit)
    {
        // TODO: Implement with System.Diagnostics.Metrics or compatible library
    }
}