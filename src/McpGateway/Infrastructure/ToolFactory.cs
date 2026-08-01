using Microsoft.Extensions.Logging;
using McpGateway.Tools.Mechanical;
using System.Net.Http;

namespace McpGateway.Infrastructure;

public interface IToolFactory
{
    Task<IEnumerable<ITool>> CreateToolsAsync();
}

public class ToolFactory : IToolFactory
{
    private readonly ILogger<ToolFactory> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public ToolFactory(
        ILogger<ToolFactory> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IEnumerable<ITool>> CreateToolsAsync()
    {
        _logger.LogInformation("Creating mechanical tools...");
        
        var tools = new List<ITool>();
        
        // Register mechanical tools
        tools.Add(new UserQueryTool(_httpClientFactory));
        tools.Add(new OrderCreateTool(_httpClientFactory));
        
        _logger.LogInformation("Created {Count} mechanical tools", tools.Count);
        
        return tools;
    }
}