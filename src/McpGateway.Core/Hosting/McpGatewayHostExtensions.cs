using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;

namespace McpGateway.Core.Hosting;

/// <summary>
/// Provides extension methods for configuring and running MCP Gateway services.
/// </summary>
public static class McpGatewayHostExtensions
{
    /// <summary>
    /// Adds MCP Gateway services to the DI container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMcpGateway(this IServiceCollection services)
    {
        // TODO: Implement service registration
        return services;
    }

    /// <summary>
    /// Runs the MCP Gateway application.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task RunMcpGatewayAsync(this WebApplication app)
    {
        // TODO: Implement gateway startup
        await app.RunAsync();
    }
}
