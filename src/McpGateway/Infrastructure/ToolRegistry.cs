using ModelContextProtocol.Server;
using Microsoft.Extensions.Logging;

namespace McpGateway.Infrastructure;

public interface IToolRegistry
{
    Task RegisterToolsAsync();
    IReadOnlyList<ITool> GetTools();
    ITool? GetTool(string name);
}

public class ToolRegistry : IToolRegistry
{
    private readonly ILogger<ToolRegistry> _logger;
    private readonly IToolFactory _toolFactory;
    private readonly List<ITool> _tools = new();

    public ToolRegistry(
        IToolFactory toolFactory,
        ILogger<ToolRegistry> logger)
    {
        _toolFactory = toolFactory;
        _logger = logger;
    }

    public async Task RegisterToolsAsync()
    {
        _logger.LogInformation("Registering MCP tools...");
        
        // 註冊 Tools - 這會在 Phase 2 和 3 中被實際的 Tools 替換
        var tools = await _toolFactory.CreateToolsAsync();
        _tools.AddRange(tools);
        
        foreach (var tool in _tools)
        {
            _logger.LogInformation("Registered tool: {ToolName}", tool.Name);
        }
        
        _logger.LogInformation("Total tools registered: {Count}", _tools.Count);
    }

    public IReadOnlyList<ITool> GetTools() => _tools.AsReadOnly();
    
    public ITool? GetTool(string name) => _tools.FirstOrDefault(t => t.Name == name);
}

// 臨時 ITool 介面，將在 Phase 2/3 中擴展
public interface ITool
{
    string Name { get; }
    string Description { get; }
    Task<dynamic> ExecuteAsync(Dictionary<string, object> parameters);
}