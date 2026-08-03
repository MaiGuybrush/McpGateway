using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using System.Reflection;

namespace McpGateway.Core.Tools;

/// <summary>
/// Extension methods for tool registration.
/// </summary>
public static class ToolRegistrationExtensions
{
    /// <summary>
    /// Adds tools from the specified assembly.
    /// </summary>
    /// <typeparam name="TMarker">A type in the assembly to scan.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddToolsFromAssembly<TMarker>(this IServiceCollection services)
    {
        // For now, this is a placeholder. The actual tool scanning logic will be implemented
        // when the SDK's tool registration mechanism is better understood.
        // See SDK spike findings in .scratch/sprint-0-sdk-spike/RESULTS.md

        return services;
    }
}
