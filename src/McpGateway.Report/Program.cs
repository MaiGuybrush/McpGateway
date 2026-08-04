using ModelContextProtocol;
using ModelContextProtocol.AspNetCore;
using McpGateway.Core.Hosting;
using McpGateway.Report.Tools.Report;
using McpGateway.Report.Tools.Test;

namespace McpGateway.Report;

class Program
{
    static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddMcpGateway();  // 註冊 MetricsService

        builder.Services.AddMcpServer()
            .WithHttpTransport(options => options.Stateless = true)  // 關鍵設定！
            .WithTools<QueryWipTool>()
            .WithTools<HelloTool>();

        var app = builder.Build();

        app.MapMcp();

        await app.RunMcpGatewayAsync();
    }
}