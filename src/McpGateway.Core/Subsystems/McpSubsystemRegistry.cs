using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ModelContextProtocol.Server;

namespace McpGateway.Core.Subsystems;

/// <summary>
/// Thread-safe default implementation of <see cref="IMcpSubsystemRegistry"/>.
/// </summary>
public class McpSubsystemRegistry : IMcpSubsystemRegistry
{
    private readonly ConcurrentDictionary<string, McpSubsystemRegistration> _subsystems =
        new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public IReadOnlyCollection<string> Subsystems => _subsystems.Keys.ToImmutableList();

    /// <inheritdoc />
    public bool ContainsSubsystem(string subsystemName)
    {
        if (string.IsNullOrWhiteSpace(subsystemName))
            return false;

        return _subsystems.ContainsKey(subsystemName.Trim());
    }

    /// <inheritdoc />
    public bool TryGetSubsystem(string subsystemName, out McpSubsystemRegistration? registration)
    {
        if (string.IsNullOrWhiteSpace(subsystemName))
        {
            registration = null;
            return false;
        }

        return _subsystems.TryGetValue(subsystemName.Trim(), out registration);
    }

    /// <inheritdoc />
    public IReadOnlySet<string> GetAllowedToolNames(string subsystemName)
    {
        if (TryGetSubsystem(subsystemName, out var reg) && reg != null)
        {
            return reg.AllowedToolNames;
        }

        return ImmutableHashSet<string>.Empty;
    }

    /// <inheritdoc />
    public McpServerPrimitiveCollection<McpServerTool> FilterToolsForSubsystem(
        string subsystemName,
        McpServerPrimitiveCollection<McpServerTool> allTools)
    {
        var filteredCollection = new McpServerPrimitiveCollection<McpServerTool>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(subsystemName) || allTools == null || allTools.IsEmpty)
        {
            return filteredCollection;
        }

        var normalizedSubsystem = subsystemName.Trim().ToLowerInvariant();

        // If subsystem is not registered, return empty collection to enforce strict isolation
        if (!TryGetSubsystem(normalizedSubsystem, out var registration) || registration == null)
        {
            return filteredCollection;
        }

        var allowedNames = registration.AllowedToolNames;

        foreach (var tool in allTools)
        {
            var toolName = tool.ProtocolTool.Name;
            if (IsToolAuthorized(normalizedSubsystem, toolName, allowedNames))
            {
                filteredCollection.Add(tool);
            }
        }

        return filteredCollection;
    }

    /// <inheritdoc />
    public void Register(McpSubsystemRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        _subsystems.AddOrUpdate(
            registration.SubsystemName,
            registration,
            (_, existing) =>
            {
                // Merge tool types and names
                foreach (var type in registration.ToolTypes)
                {
                    existing.AddToolType(type);
                }
                foreach (var name in registration.AllowedToolNames)
                {
                    existing.AddToolName(name);
                }
                return existing;
            });
    }

    /// <summary>
    /// Determines whether a tool name is authorized for a specific subsystem.
    /// </summary>
    public static bool IsToolAuthorized(
        string subsystemName,
        string toolName,
        IReadOnlySet<string>? explicitAllowedNames = null)
    {
        if (string.IsNullOrWhiteSpace(toolName) || string.IsNullOrWhiteSpace(subsystemName))
            return false;

        var normalizedTool = toolName.Trim().ToLowerInvariant();
        var normalizedSubsystem = subsystemName.Trim().ToLowerInvariant();

        // 1. Check explicit whitelist
        if (explicitAllowedNames != null && explicitAllowedNames.Contains(normalizedTool))
        {
            return true;
        }

        // 2. Check {department}_{system}_{action} standard convention
        var segments = normalizedTool.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 3)
        {
            // Second segment is system
            if (segments[1].Equals(normalizedSubsystem, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        else if (segments.Length == 2)
        {
            // {system}_{action}
            if (segments[0].Equals(normalizedSubsystem, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
