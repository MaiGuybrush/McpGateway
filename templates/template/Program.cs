using McpGateway.Core.Hosting;
using McpGateway.__Department__.Configuration;
using McpGateway.__Department__.Services;
using McpGateway.__Department__.Tools.__ToolClass__;
using McpGateway.__Department__.Tools.Test;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

// 1. 註冊 McpGateway 核心服務
builder.Services.AddMcpGateway();

// 2. 註冊業務 Options 與 Services
builder.Services.Configure<__OptionsClass__>(
    builder.Configuration.GetSection(__OptionsClass__.SectionName));
builder.Services.AddHttpClient<I__ToolClass__Service, __ToolClass__Service>();
builder.Services.AddSingleton<IShopConfigResolver, ConsulShopConfigResolver>();

// 3. 註冊 MCP Tools
builder.Services.AddMcpServer()
    .WithTools<__ToolClass__Tool>()
    .WithTools<HelloTool>();

var app = builder.Build();

// 4. 掛載路由與啟動
app.MapMcpGateway();
await app.RunMcpGatewayAsync();
