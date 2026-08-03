using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using McpGateway.Core.Configuration;
using McpGateway.Core.Observability;

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
        // Register configuration
        services.AddOptions<McpGatewayConfig>()
            .BindConfiguration("McpGateway")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Register health checks
        services.AddHealthChecks()
            .AddCheck<GatewayHealthChecks>("gateway", tags: new[] { "live", "ready" });

        // Register MCP services
        services.AddMcpServer(options =>
        {
            // Minimal configuration for gateway
        })
        .WithHttpTransport(httpOptions => httpOptions.Stateless = true);

        return services;
    }

    /// <summary>
    /// Maps MCP Gateway endpoints to the application.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for chaining.</returns>
    public static WebApplication MapMcpGateway(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILogger<WebApplication>>();
        
        // Get configuration
        var config = app.Services.GetRequiredService<IOptions<McpGatewayConfig>>().Value;
        
        logger.LogInformation("Mapping MCP Gateway endpoints: RoutePrefix={RoutePrefix}, EnableHealthChecks={EnableHealthChecks}", 
            config.RoutePrefix, config.EnableHealthChecks);

        // Map health check endpoints
        if (config.EnableHealthChecks)
        {
            logger.LogInformation("Mapping health check endpoints");
            app.MapHealthChecks("/health/live", new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("live")
            });
            
            app.MapHealthChecks("/health/ready", new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("ready")
            });
        }

        // Map MCP endpoint
        logger.LogInformation("Mapping MCP endpoint at {RoutePrefix}", config.RoutePrefix);
        app.MapMcp(config.RoutePrefix);

        return app;
    }

    /// <summary>
    /// Runs the MCP Gateway application.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task RunMcpGatewayAsync(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILogger<WebApplication>>();
        
        // Get configuration
        var config = app.Services.GetRequiredService<IOptions<McpGatewayConfig>>().Value;
        
        logger.LogInformation("MCP Gateway configuration: RoutePrefix={RoutePrefix}, EnableHealthChecks={EnableHealthChecks}", 
            config.RoutePrefix, config.EnableHealthChecks);

        // Run the application
        logger.LogInformation("Starting MCP Gateway...");
        await app.RunAsync();
    }
}
