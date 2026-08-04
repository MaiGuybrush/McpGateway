using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using McpGateway.Core.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace McpGateway.Core.Hosting;

/// <summary>
/// Extension methods for adding tools from assemblies.
/// </summary>
public static class McpGatewayToolExtensions
{
    /// <summary>
    /// Adds tools from the specified assembly to the MCP server.
    /// </summary>
    /// <typeparam name="TMarker">A type in the assembly to scan.</typeparam>
    /// <param name="builder">The MCP server builder.</param>
    /// <returns>The MCP server builder for chaining.</returns>
    public static IMcpServerBuilder WithToolsFromAssembly<TMarker>(this IMcpServerBuilder builder)
    {
        var assembly = typeof(TMarker).Assembly;
        return builder.WithToolsFromAssembly(assembly);
    }

    /// <summary>
    /// Adds tools from the specified assembly to the MCP server.
    /// </summary>
    /// <param name="builder">The MCP server builder.</param>
    /// <param name="assembly">The assembly to scan for tools.</param>
    /// <returns>The MCP server builder for chaining.</returns>
    public static IMcpServerBuilder WithToolsFromAssembly(this IMcpServerBuilder builder, Assembly assembly)
    {
        // Find all tool classes marked with [McpTool]
        var toolTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.GetCustomAttribute<McpToolAttribute>() != null)
            .Where(t => t.BaseType?.IsGenericType == true && t.BaseType.GetGenericTypeDefinition() == typeof(ToolBase<,>))
            .ToList();

        // Register each tool with the MCP server
        foreach (var toolType in toolTypes)
        {
            builder = builder.WithTools(toolType);
        }

        return builder;
    }
}

/// <summary>
/// Attribute to mark a tool class for automatic registration.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class McpToolAttribute : Attribute
{
    /// <summary>
    /// Gets the tool name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets or sets the tool version.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets a value indicating whether the tool is deprecated.
    /// </summary>
    public bool Deprecated { get; set; } = false;

    /// <summary>
    /// Gets or sets the deprecation reason.
    /// </summary>
    public string? DeprecationReason { get; set; }

    /// <summary>
    /// Initializes a new instance of the McpToolAttribute.
    /// </summary>
    /// <param name="name">The tool name.</param>
    public McpToolAttribute(string name)
    {
        Name = name;
    }
}