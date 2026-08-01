using ModelContextProtocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using McpGateway.Tools;
using McpGateway.Infrastructure;

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureServices((hostContext, services) =>
{
    services.AddHttpClient();
    
    services.AddSingleton<IMcpServer, McpServer>();
    services.AddSingleton<IToolRegistry, ToolRegistry>();
    services.AddSingleton<IToolFactory, ToolFactory>();
    
    services.AddHostedService<McpGatewayService>();
});

await builder.RunConsoleAsync();