using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace McpGateway.Core.Subsystems;

/// <summary>
/// Extension methods for registering MCP subsystems into the dependency injection container.
/// </summary>
public static class McpSubsystemServiceCollectionExtensions
{
    /// <summary>
    /// Adds and configures an MCP subsystem with its dedicated tools and isolation whitelist.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="subsystemName">The subsystem identifier (e.g. "mes", "wms").</param>
    /// <param name="configure">The builder action to configure tools.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMcpSubsystem(
        this IServiceCollection services,
        string subsystemName,
        Action<McpSubsystemBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (string.IsNullOrWhiteSpace(subsystemName))
            throw new ArgumentException("Subsystem name cannot be null or empty.", nameof(subsystemName));
        ArgumentNullException.ThrowIfNull(configure);

        var registry = GetOrAddSubsystemRegistry(services);

        var builder = new McpSubsystemBuilder(subsystemName, services);
        configure(builder);

        var registration = builder.Build();
        registry.Register(registration);

        return services;
    }

    /// <summary>
    /// Retrieves or registers the singleton <see cref="IMcpSubsystemRegistry"/> in the service collection.
    /// </summary>
    internal static McpSubsystemRegistry GetOrAddSubsystemRegistry(IServiceCollection services)
    {
        var descriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(IMcpSubsystemRegistry));
        if (descriptor?.ImplementationInstance is McpSubsystemRegistry existingInstance)
        {
            return existingInstance;
        }

        var newRegistry = new McpSubsystemRegistry();
        if (descriptor != null)
        {
            services.Remove(descriptor);
        }

        services.AddSingleton<IMcpSubsystemRegistry>(newRegistry);
        return newRegistry;
    }
}
