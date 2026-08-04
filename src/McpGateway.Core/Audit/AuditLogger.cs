using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace McpGateway.Core.Audit;

/// <summary>
/// Audit logger for PII-redacted tool invocations and responses.
/// Implements ADR-006 audit requirements.
/// </summary>
public class AuditLogger
{
    private readonly ILogger<AuditLogger> _logger;
    private readonly PiiRedactor _redactor;
    private readonly string _sink;
    private readonly string _department;
    private readonly string _coreVersion;
    private readonly JsonSerializerOptions _jsonOptions;

    public AuditLogger(
        ILogger<AuditLogger> logger, 
        PiiRedactor redactor, 
        string sink,
        string department,
        string coreVersion,
        JsonSerializerOptions? jsonOptions = null)
    {
        _logger = logger;
        _redactor = redactor;
        _sink = sink;
        _department = department;
        _coreVersion = coreVersion;
        _jsonOptions = jsonOptions ?? new JsonSerializerOptions 
        { 
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <summary>
    /// Logs a tool invocation with PII redaction.
    /// </summary>
    /// <param name="log">The audit log entry.</param>
    public void LogToolInvocation(ToolInvocationAuditLog log)
    {
        var redactedLog = log with { Parameters = (Dictionary<string, object?>)_redactor.Redact(log.Parameters) };
        
        switch (_sink.ToLowerInvariant())
        {
            case "applicationinsights":
            case "appinsights":
            case "ai":
                LogToApplicationInsights(redactedLog);
                break;
            case "file":
                LogToFile(redactedLog);
                break;
            case "console":
            case "stdout":
                LogToConsole(redactedLog);
                break;
            default:
                LogToConsole(redactedLog); // Default fallback
                break;
        }
    }

    private void LogToApplicationInsights(ToolInvocationAuditLog log)
    {
        // For Application Insights, we log as structured data
        _logger.LogInformation(
            "ToolInvocation: {@ToolLog}",
            JsonSerializer.Serialize(log, _jsonOptions));
    }

    private void LogToFile(ToolInvocationAuditLog log)
    {
        // For file logging, we log as JSON
        var json = JsonSerializer.Serialize(log, _jsonOptions);
        _logger.LogInformation("AUDIT: {AuditJson}", json);
    }

    private void LogToConsole(ToolInvocationAuditLog log)
    {
        // For console, log formatted for readability
        _logger.LogInformation(
            "[{Timestamp}] Tool={ToolName} Agent={AgentId} Success={Success} Duration={DurationMs}ms",
            log.Timestamp,
            log.ToolName,
            log.AgentId,
            log.Success,
            log.DurationMs);
        
        if (!log.Success && !string.IsNullOrEmpty(log.ErrorMessage))
        {
            _logger.LogInformation("  Error: {ErrorMessage}", log.ErrorMessage);
        }
    }
}


