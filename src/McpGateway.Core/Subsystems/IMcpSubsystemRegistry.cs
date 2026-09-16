using System;
using System.Collections.Generic;
using ModelContextProtocol.Server;

namespace McpGateway.Core.Subsystems;

/// <summary>
/// Registry contract for managing subsystem-specific tool whitelists and dynamic filtering.
/// Implements ADR-014 subsystem endpoint routing and tool isolation.
/// </summary>
public interface IMcpSubsystemRegistry
{
    /// <summary>
    /// Gets all registered subsystem names.
    /// </summary>
    IReadOnlyCollection<string> Subsystems { get; }

    /// <summary>
    /// Checks whether the specified subsystem is registered.
    /// </summary>
    /// <param name="subsystemName">The subsystem identifier.</param>
    /// <returns>True if registered; otherwise false.</returns>
    bool ContainsSubsystem(string subsystemName);

    /// <summary>
    /// Tries to get the subsystem registration by name.
    /// </summary>
    /// <param name="subsystemName">The subsystem identifier.</param>
    /// <param name="registration">The subsystem registration if found.</param>
    /// <returns>True if found; otherwise false.</returns>
    bool TryGetSubsystem(string subsystemName, out McpSubsystemRegistration? registration);

    /// <summary>
    /// Gets the allowed tool names for the specified subsystem.
    /// </summary>
    /// <param name="subsystemName">The subsystem identifier.</param>
    /// <returns>The set of allowed tool names.</returns>
    IReadOnlySet<string> GetAllowedToolNames(string subsystemName);

    /// <summary>
    /// Filters the global tool collection to include only tools belonging to the specified subsystem.
    /// </summary>
    /// <param name="subsystemName">The subsystem identifier.</param>
    /// <param name="allTools">The incoming full tool collection.</param>
    /// <returns>A new tool collection containing only tools authorized for this subsystem.</returns>
    McpServerPrimitiveCollection<McpServerTool> FilterToolsForSubsystem(
        string subsystemName,
        McpServerPrimitiveCollection<McpServerTool> allTools);

    /// <summary>
    /// Registers a subsystem metadata descriptor into the registry.
    /// </summary>
    /// <param name="registration">The subsystem registration.</param>
    void Register(McpSubsystemRegistration registration);
}
