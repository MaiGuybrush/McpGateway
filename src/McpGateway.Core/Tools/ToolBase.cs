using Microsoft.Extensions.DependencyInjection;

namespace McpGateway.Core.Tools;

/// <summary>
/// Base class for implementing MCP tools with dependency injection support.
/// </summary>
/// <typeparam name="TInput">The input type.</typeparam>
/// <typeparam name="TOutput">The output type.</typeparam>
public abstract class ToolBase<TInput, TOutput> /* : ITool */
{
    /// <summary>
    /// Gets the HTTP client factory for making downstream API calls.
    /// </summary>
    protected internal readonly IHttpClientFactory HttpClientFactory;

    /// <summary>
    /// Initializes a new instance of the tool.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    protected ToolBase(IHttpClientFactory httpClientFactory)
    {
        HttpClientFactory = httpClientFactory;
    }

    // TODO: Implement ITool interface when SDK contracts are finalized
    // Task<CallToolResult> InvokeAsync(...);
}
