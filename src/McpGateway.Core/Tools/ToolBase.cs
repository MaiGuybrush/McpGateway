using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace McpGateway.Core.Tools;

/// <summary>
/// Context information passed to tools during execution.
/// </summary>
/// <param name="UserId">The user identifier.</param>
/// <param name="Department">The department name.</param>
/// <param name="Role">The user role.</param>
/// <param name="TokenType">The token type.</param>
/// <param name="AgentId">The agent identifier.</param>
/// <param name="CorrelationId">The correlation identifier for request tracking.</param>
public record ToolContext(
    string UserId,
    string Department,
    string Role,
    string TokenType,
    string AgentId,
    string CorrelationId);

/// <summary>
/// Base class for implementing MCP tools with dependency injection support.
/// </summary>
/// <typeparam name="TInput">The input type.</typeparam>
/// <typeparam name="TOutput">The output type.</typeparam>
public abstract class ToolBase<TInput, TOutput>
{
    /// <summary>
    /// Gets the downstream client for making API calls with automatic header injection and retry logic.
    /// </summary>
    // Phase 3: Uncomment when IDownstreamClient is implemented
    // protected internal readonly IDownstreamClient Downstream;

    /// <summary>
    /// Gets the JSON serializer options.
    /// </summary>
    protected internal readonly JsonSerializerOptions JsonSerializerOptions;

    /// <summary>
    /// Initializes a new instance of the tool.
    /// </summary>
    /// <param name="downstreamClient">The downstream client for API calls.</param>
    // Phase 3: Uncomment when IDownstreamClient is implemented
    // protected ToolBase(IDownstreamClient downstreamClient)
    protected ToolBase()
    {
        // Phase 3: Uncomment when IDownstreamClient is implemented
        // Downstream = downstreamClient;
        JsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <summary>
    /// Initializes a new instance of the tool (legacy constructor for backward compatibility).
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    [Obsolete("Use constructor accepting IDownstreamClient instead")]
    protected ToolBase(IHttpClientFactory httpClientFactory)
    {
        // Temporary fallback for existing tools
        JsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <summary>
    /// Gets the tool name.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Gets the tool description.
    /// </summary>
    public abstract string Description { get; }

    /// <summary>
    /// Executes the tool with the given input and context.
    /// </summary>
    /// <param name="input">The input parameters.</param>
    /// <param name="context">The tool context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The execution result.</returns>
    public abstract Task<TOutput> ExecuteAsync(TInput input, ToolContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the tool with the given arguments.
    /// </summary>
    /// <param name="arguments">The input arguments as JSON string.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The execution result.</returns>
    public async Task<TOutput> ExecuteAsync(string? arguments, CancellationToken cancellationToken = default)
    {
        var input = arguments != null 
            ? System.Text.Json.JsonSerializer.Deserialize<TInput>(arguments, JsonSerializerOptions)!
            : Activator.CreateInstance<TInput>();

        // Create dummy context for now (no auth in this sprint)
        var context = new ToolContext(
            UserId: "anonymous",
            Department: "unknown",
            Role: "anonymous",
            TokenType: "none",
            AgentId: "unknown",
            CorrelationId: Guid.NewGuid().ToString()
        );

        return await ExecuteAsync(input, context, cancellationToken);
    }
}

/// <summary>
/// Attribute to mark a tool class for automatic registration.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class McpToolAttribute : Attribute
{
    /// <summary>
    /// Gets the tool name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets or sets the tool version.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets a value indicating whether the tool is deprecated.
    /// </summary>
    public bool Deprecated { get; set; } = false;

    /// <summary>
    /// Gets or sets the deprecation reason.
    /// </summary>
    public string? DeprecationReason { get; set; }

    /// <summary>
    /// Initializes a new instance of the McpToolAttribute.
    /// </summary>
    /// <param name="name">The tool name.</param>
    public McpToolAttribute(string name)
    {
        Name = name;
    }
}