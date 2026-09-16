using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using ModelContextProtocol.Server;

namespace McpGateway.Core.Subsystems;

/// <summary>
/// Represents the registration metadata and tool whitelist for an MCP subsystem.
/// </summary>
public class McpSubsystemRegistration
{
    private readonly HashSet<string> _allowedToolNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Type> _toolTypes = new();

    /// <summary>
    /// Gets the unique subsystem name (e.g. "mes", "wms").
    /// </summary>
    public string SubsystemName { get; }

    /// <summary>
    /// Gets the registered tool types for this subsystem.
    /// </summary>
    public IReadOnlyCollection<Type> ToolTypes => _toolTypes.AsReadOnly();

    /// <summary>
    /// Gets the allowed tool names for this subsystem.
    /// </summary>
    public IReadOnlySet<string> AllowedToolNames => _allowedToolNames;

    /// <summary>
    /// Initializes a new instance of <see cref="McpSubsystemRegistration"/>.
    /// </summary>
    /// <param name="subsystemName">The subsystem identifier.</param>
    /// <param name="toolTypes">The tool types associated with the subsystem.</param>
    /// <param name="explicitToolNames">Explicit tool names to whitelist.</param>
    public McpSubsystemRegistration(
        string subsystemName,
        IEnumerable<Type>? toolTypes = null,
        IEnumerable<string>? explicitToolNames = null)
    {
        if (string.IsNullOrWhiteSpace(subsystemName))
        {
            throw new ArgumentException("Subsystem name cannot be null or empty.", nameof(subsystemName));
        }

        SubsystemName = subsystemName.Trim().ToLowerInvariant();

        if (toolTypes != null)
        {
            foreach (var type in toolTypes)
            {
                AddToolType(type);
            }
        }

        if (explicitToolNames != null)
        {
            foreach (var name in explicitToolNames)
            {
                if (!string.IsNullOrWhiteSpace(name))
                {
                    _allowedToolNames.Add(name.Trim().ToLowerInvariant());
                }
            }
        }
    }

    /// <summary>
    /// Adds a tool type and extracts its MCP tool names.
    /// </summary>
    /// <param name="toolType">The tool type to add.</param>
    public void AddToolType(Type toolType)
    {
        ArgumentNullException.ThrowIfNull(toolType);

        if (!_toolTypes.Contains(toolType))
        {
            _toolTypes.Add(toolType);
        }

        ExtractToolNamesFromType(toolType);
    }

    /// <summary>
    /// Adds an explicit tool name to the whitelist.
    /// </summary>
    /// <param name="toolName">The tool name to allow.</param>
    public void AddToolName(string toolName)
    {
        if (!string.IsNullOrWhiteSpace(toolName))
        {
            _allowedToolNames.Add(toolName.Trim().ToLowerInvariant());
        }
    }

    private void ExtractToolNamesFromType(Type type)
    {
        // 1. Check class-level attributes
        foreach (var attr in type.GetCustomAttributes())
        {
            var attrName = attr.GetType().Name;
            if (attrName is "McpToolAttribute" or "McpServerToolAttribute")
            {
                var name = attr.GetType().GetProperty("Name")?.GetValue(attr) as string;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    _allowedToolNames.Add(name.Trim().ToLowerInvariant());
                }
            }
        }

        // 2. Check methods for tool attributes
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
        {
            foreach (var attr in method.GetCustomAttributes())
            {
                var attrName = attr.GetType().Name;
                if (attrName is "McpToolAttribute" or "McpServerToolAttribute")
                {
                    var name = attr.GetType().GetProperty("Name")?.GetValue(attr) as string;
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        _allowedToolNames.Add(name.Trim().ToLowerInvariant());
                    }
                    else
                    {
                        _allowedToolNames.Add(method.Name.Trim().ToLowerInvariant());
                    }
                }
            }
        }
    }
}
