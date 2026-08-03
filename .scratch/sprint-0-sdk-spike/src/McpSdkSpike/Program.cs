using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

// Add MCP services with proper stateless configuration
builder.Services.AddMcpServer(options =>
{
    // For spike testing, enable stateless mode
})
.WithHttpTransport(httpOptions => httpOptions.Stateless = true)
.WithTools<Tools>();

var app = builder.Build();

// Map MCP at path prefix /test
app.MapMcp("/test");

app.Run();

public class Tools
{
    public static string Echo(string message) => $"Echo: {message}";
    public static int Add(int a, int b) => a + b;
}
