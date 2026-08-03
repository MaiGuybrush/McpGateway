namespace McpGateway.Core.Configuration;

/// <summary>
/// Configuration options for MCP Gateway.
/// </summary>
public class McpGatewayConfig
{
    /// <summary>
    /// Gets or sets the route prefix for MCP endpoints.
    /// </summary>
    public string RoutePrefix { get; set; } = "/mcp";
    
    /// <summary>
    /// Gets or sets whether to enable health check endpoints.
    /// </summary>
    public bool EnableHealthChecks { get; set; } = true;
}