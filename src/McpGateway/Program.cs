using McpGateway.Core.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMcpGateway();

var app = builder.Build();
app.MapMcpGateway();
await app.RunMcpGatewayAsync();