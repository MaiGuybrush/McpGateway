using Microsoft.Extensions.Hosting;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Microsoft.Extensions.Logging;

namespace McpGateway.Infrastructure;

public class McpGatewayService : BackgroundService
{
    private readonly IMcpServer _mcpServer;
    private readonly IToolRegistry _toolRegistry;
    private readonly ILogger<McpGatewayService> _logger;

    public McpGatewayService(
        IMcpServer mcpServer,
        IToolRegistry toolRegistry,
        ILogger<McpServer> logger)
    {
        _mcpServer = mcpServer;
        _toolRegistry = toolRegistry;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MCP Gateway Service starting...");
        
        try
        {
            // 註冊所有 Tools
            await _toolRegistry.RegisterToolsAsync();
            
            // 啟動 MCP Server
            await _mcpServer.StartAsync(stoppingToken);
            
            _logger.LogInformation("MCP Gateway Service started successfully");
            
            // 等待取消
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("MCP Gateway Service is stopping");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MCP Gateway Service encountered an error");
            throw;
        }
    }
}