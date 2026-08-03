using McpGateway.Core.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMcpGateway();               // auth + audit + transport + validation

var app = builder.Build();
app.MapMcp("/report");
await app.RunMcpGatewayAsync();                 // startup validation + run
