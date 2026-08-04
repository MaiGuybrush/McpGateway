namespace McpGateway.Core.Audit;

/// <summary>
/// Audit log record for tool invocations with PII protection.
/// Meets Core spec §8 requirements.
/// </summary>
public sealed record ToolInvocationAuditLog
{
    public DateTimeOffset Timestamp { get; init; }
    public string AgentId { get; init; } = string.Empty;
    public string ToolName { get; init; } = string.Empty;
    public string? ToolVersion { get; init; }
    public string Department { get; init; } = string.Empty;
    public string CoreVersion { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
    public Dictionary<string, object?> Parameters { get; init; } = new();
    public bool Success { get; init; }
    public int? HttpStatusCode { get; init; }
    public long DurationMs { get; init; }
    public string? ErrorMessage { get; init; }
}