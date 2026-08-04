using McpGateway.Core.Hosting;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMcpGateway();

var app = builder.Build();

// Map metrics endpoint
app.MapMetrics("/metrics");

app.MapMcpGateway();
await app.RunMcpGatewayAsync();