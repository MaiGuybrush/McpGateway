using Prometheus;
using System;
using System.Collections.Generic;

namespace McpGateway.Core.Observability;

/// <summary>
/// Service for exposing Prometheus metrics from the MCP Gateway.
/// Implements all required metrics from sprint-3 specification.
/// Uses prometheus-net 8.2.1 for Prometheus integration.
/// </summary>
public class MetricsService
{
    private static readonly string Version = typeof(MetricsService).Assembly.GetName().Version?.ToString() ?? "0.1.0";
    
    // Metric definitions
    private static readonly Gauge CoreVersion = Metrics.CreateGauge(
        "mcpgw_core_version",
        "MCP Gateway Core version information",
        new GaugeConfiguration
        {
            LabelNames = new[] { "dept", "version" }
        }
    );

    private static readonly Counter ToolCallsTotal = Metrics.CreateCounter(
        "mcpgw_tool_calls_total",
        "Total number of tool calls",
        new CounterConfiguration
        {
            LabelNames = new[] { "dept", "tool", "status" }
        }
    );

    private static readonly Histogram ToolDurationSeconds = Metrics.CreateHistogram(
        "mcpgw_tool_duration_seconds",
        "Tool execution duration in seconds",
        new HistogramConfiguration
        {
            LabelNames = new[] { "dept", "tool" },
            Buckets = Histogram.LinearBuckets(0.1, 0.1, 20) // 0.1s to 2.0s
        }
    );

    private static readonly Histogram DownstreamDurationSeconds = Metrics.CreateHistogram(
        "mcpgw_downstream_duration_seconds",
        "Downstream HTTP request duration in seconds",
        new HistogramConfiguration
        {
            LabelNames = new[] { "dept", "tool" },
            Buckets = Histogram.LinearBuckets(0.05, 0.05, 20) // 0.05s to 1.0s
        }
    );

    private static readonly Counter AuthFailuresTotal = Metrics.CreateCounter(
        "mcpgw_auth_failures_total",
        "Total number of authentication failures",
        new CounterConfiguration
        {
            LabelNames = new[] { "dept", "reason" }
        }
    );

    private static readonly Counter TokenCacheTotal = Metrics.CreateCounter(
        "mcpgw_token_cache_total",
        "Total token cache operations",
        new CounterConfiguration
        {
            LabelNames = new[] { "dept", "result" }
        }
    );

    private readonly string _department;

    public MetricsService(string department)
    {
        _department = department ?? "unknown";
        
        // Set version gauge - version is a label, not a static label
        CoreVersion.WithLabels(_department, Version).Set(1);
    }

    /// <summary>
    /// Records a tool call with its outcome and duration.
    /// </summary>
    /// <param name="toolName">Name of the tool that was called.</param>
    /// <param name="success">Whether the tool call succeeded.</param>
    /// <param name="duration">Duration of the tool call.</param>
    public void RecordToolCall(string toolName, bool success, TimeSpan duration)
    {
        var status = success ? "success" : "failure";
        ToolCallsTotal.WithLabels(_department, toolName, status).Inc();
        ToolDurationSeconds.WithLabels(_department, toolName).Observe(duration.TotalSeconds);
    }

    /// <summary>
    /// Records a downstream HTTP request duration.
    /// </summary>
    /// <param name="toolName">Name of the tool making the request.</param>
    /// <param name="duration">Duration of the downstream request.</param>
    public void RecordDownstreamRequest(string toolName, TimeSpan duration)
    {
        DownstreamDurationSeconds.WithLabels(_department, toolName).Observe(duration.TotalSeconds);
    }

    /// <summary>
    /// Records an authentication failure.
    /// </summary>
    /// <param name="reason">Reason for the authentication failure (invalid_token, timeout, etc.).</param>
    public void RecordAuthFailure(string reason)
    {
        AuthFailuresTotal.WithLabels(_department, reason).Inc();
    }

    /// <summary>
    /// Records a token cache operation.
    /// </summary>
    /// <param name="hit">Whether the cache lookup was a hit (true) or miss (false).</param>
    public void RecordTokenCacheOperation(bool hit)
    {
        var result = hit ? "hit" : "miss";
        TokenCacheTotal.WithLabels(_department, result).Inc();
    }
}