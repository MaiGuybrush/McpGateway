using ModelContextProtocol.Server;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Protocol.Transport;
using ModelContextProtocol.Protocol;
using System.ComponentModel;

namespace McpGateway.Infrastructure;

public class McpServerHost
{
    private readonly McpServerOptions _options;
    private readonly List<ITool> _tools = new();

    public McpServerHost(McpServerOptions? options = null)
    {
        _options = options ?? new McpServerOptions
        {
            ServerInfo = new Implementation
            {
                Name = "McpGateway",
                Version = "1.0.0"
            }
        };
    }

    public void RegisterTool<T>() where T : ITool, new()
    {
        var tool = new T();
        _tools.Add(tool);
    }

    public void RegisterTool(ITool tool)
    {
        _tools.Add(tool);
    }

    public async Task StartAsync(ITransport transport, CancellationToken cancellationToken = default)
    {
        var server = await McpServer.ForTransport(transport, _options, cancellationToken);
        
        // Register all tools
        foreach (var tool in _tools)
        {
            await server.RegisterToolAsync(tool, cancellationToken);
        }

        await server.StartAsync(cancellationToken);
    }

    public static async Task<McpServer> CreateAsync(
        McpServerOptions options,
        ITransport transport,
        IEnumerable<ITool> tools,
        CancellationToken cancellationToken = default)
    {
        var server = await McpServer.ForTransport(transport, options, cancellationToken);
        
        foreach (var tool in tools)
        {
            await server.RegisterToolAsync(tool, cancellationToken);
        }

        return server;
    }
}

// Base tool interface
public interface ITool
{
    string Name { get; }
    string Description { get; }
    Task<CallToolResponse> ExecuteAsync(Dictionary<string, object> arguments);
}

// Base tool implementation
public abstract class ToolBase : ITool
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract Task<CallToolResponse> ExecuteAsync(Dictionary<string, object> arguments);

    protected CallToolResponse Success(object? result = null, string? message = null)
    {
        return new CallToolResponse
        {
            Content = new List<Content> 
            {
                new Content
                {
                    Text = message ?? "Operation completed successfully",
                    Type = "text"
                }
            },
            IsError = false,
            Result = result
        };
    }

    protected CallToolResponse Error(string errorMessage)
    {
        return new CallToolResponse
        {
            Content = new List<Content>
            {
                new Content
                {
                    Text = errorMessage,
                    Type = "text"
                }
            },
            IsError = true
        };
    }

    protected T? GetArgument<T>(Dictionary<string, object> arguments, string key, T? defaultValue = default)
    {
        if (arguments.TryGetValue(key, out var value))
        {
            try
            {
                return (T?)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }
        return defaultValue;
    }
}