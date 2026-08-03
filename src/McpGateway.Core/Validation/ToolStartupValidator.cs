using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using McpGateway.Core.Configuration;
using McpGateway.Core.Tools;

namespace McpGateway.Core.Validation;

/// <summary>
/// Validates tools during startup according to the 6 validation rules.
/// </summary>
public class ToolStartupValidator : IStartupValidator
{
    private readonly ILogger<ToolStartupValidator> _logger;
    private readonly IOptions<McpGatewayOptions> _options;
    private readonly Assembly[] _assembliesToScan;

    /// <summary>
    /// Initializes a new instance of the ToolStartupValidator.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="options">The gateway options.</param>
    /// <param name="assembliesToScan">The assemblies to scan for tools.</param>
    public ToolStartupValidator(
        ILogger<ToolStartupValidator> logger,
        IOptions<McpGatewayOptions> options,
        params Assembly[] assembliesToScan)
    {
        _logger = logger;
        _options = options;
        _assembliesToScan = assembliesToScan;
    }

    /// <summary>
    /// Validates all tools according to the 6 validation rules.
    /// </summary>
    public IReadOnlyList<ValidationError> Validate()
    {
        var errors = new List<ValidationError>();
        var tools = DiscoverTools();

        // Validation #1: Classes have [McpTool] attribute
        ValidateMcpToolAttribute(tools, errors);

        // Validation #2: TInput properties have [Description] attribute
        ValidateInputDescriptions(tools, errors);

        // Validation #3: [McpTool].Name starts with {Department}_
        ValidateDepartmentPrefix(tools, errors);

        // Validation #4: Tool names are unique within the service
        ValidateUniqueNames(tools, errors);

        // Validation #5: Version must be in SemVer format if present
        ValidateSemVerVersions(tools, errors);

        // Validation #6: Orphan description override paths (warning only)
        ValidateOrphanDescriptionPaths(tools, errors);

        return errors.AsReadOnly();
    }

    private List<Type> DiscoverTools()
    {
        var tools = new List<Type>();

        foreach (var assembly in _assembliesToScan)
        {
            var toolTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract &&
                           (t.GetCustomAttribute<McpToolAttribute>() != null ||
                            (t.BaseType?.IsGenericType == true && 
                             t.BaseType.GetGenericTypeDefinition() == typeof(ToolBase<,>))))
                .ToList();

            tools.AddRange(toolTypes);
        }

        return tools;
    }

    private void ValidateMcpToolAttribute(List<Type> tools, List<ValidationError> errors)
    {
        foreach (var tool in tools)
        {
            var attribute = tool.GetCustomAttribute<McpToolAttribute>();
            if (attribute == null)
            {
                errors.Add(new ValidationError
                {
                    ToolName = tool.Name,
                    FileLocation = tool.Assembly.Location,
                    Description = "Tool class must be decorated with [McpTool] attribute"
                });
            }
        }
    }

    private void ValidateInputDescriptions(List<Type> tools, List<ValidationError> errors)
    {
        foreach (var tool in tools)
        {
            var baseType = tool.BaseType;
            if (baseType?.IsGenericType != true || 
                baseType.GetGenericTypeDefinition() != typeof(ToolBase<,>))
            {
                continue;
            }

            var inputType = baseType.GetGenericArguments()[0];
            var propertiesWithoutDescription = inputType.GetProperties()
                .Where(p => p.GetCustomAttribute<DescriptionAttribute>() == null)
                .Select(p => p.Name)
                .ToList();

            if (propertiesWithoutDescription.Any())
            {
                errors.Add(new ValidationError
                {
                    ToolName = tool.Name,
                    FileLocation = $"{tool.Assembly.Location}::{inputType.FullName}",
                    Description = $"TInput properties missing [Description] attribute: {string.Join(", ", propertiesWithoutDescription)}"
                });
            }
        }
    }

    private void ValidateDepartmentPrefix(List<Type> tools, List<ValidationError> errors)
    {
        var department = _options.Value.Department?.ToLowerInvariant() ?? string.Empty;
        var expectedPrefix = $"{department}_";

        foreach (var tool in tools)
        {
            var attribute = tool.GetCustomAttribute<McpToolAttribute>();
            if (attribute == null) continue;

            if (!attribute.Name.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new ValidationError
                {
                    ToolName = tool.Name,
                    FileLocation = tool.Assembly.Location,
                    Description = $"Tool name '{attribute.Name}' must start with '{expectedPrefix}' (department prefix as per ADR-009 D6)"
                });
            }
        }
    }

    private void ValidateUniqueNames(List<Type> tools, List<ValidationError> errors)
    {
        var toolNames = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var tool in tools)
        {
            var attribute = tool.GetCustomAttribute<McpToolAttribute>();
            if (attribute == null) continue;

            if (!toolNames.ContainsKey(attribute.Name))
            {
                toolNames[attribute.Name] = new List<string>();
            }
            toolNames[attribute.Name].Add(tool.FullName!);
        }

        foreach (var kvp in toolNames.Where(kvp => kvp.Value.Count > 1))
        {
            errors.Add(new ValidationError
            {
                ToolName = kvp.Key,
                FileLocation = "Multiple locations",
                Description = $"Tool name '{kvp.Key}' is not unique. Found in: {string.Join(", ", kvp.Value)}"
            });
        }
    }

    private void ValidateSemVerVersions(List<Type> tools, List<ValidationError> errors)
    {
        var semVerPattern = new Regex(@"^\d+\.\d+\.\d+(?:-[a-zA-Z0-9-]+(?:\.[a-zA-Z0-9-]+)*)?(?:\+[a-zA-Z0-9-]+(?:\.[a-zA-Z0-9-]+)*)?$");

        foreach (var tool in tools)
        {
            var attribute = tool.GetCustomAttribute<McpToolAttribute>();
            if (attribute == null) continue;

            if (!string.IsNullOrEmpty(attribute.Version) && !semVerPattern.IsMatch(attribute.Version))
            {
                errors.Add(new ValidationError
                {
                    ToolName = tool.Name,
                    FileLocation = tool.Assembly.Location,
                    Description = $"Tool version '{attribute.Version}' must be in SemVer format (e.g., 1.0.0, 2.1.0-beta.1)"
                });
            }
        }
    }

    private void ValidateOrphanDescriptionPaths(List<Type> tools, List<ValidationError> errors)
    {
        // This validation only logs warnings, doesn't add to errors list
        foreach (var tool in tools)
        {
            var xmlDocFile = Path.ChangeExtension(tool.Assembly.Location, ".xml");
            if (!File.Exists(xmlDocFile))
            {
                _logger.LogWarning("Tool {ToolName} has no XML documentation file at {XmlPath}. Consider adding XML documentation for better tool descriptions",
                    tool.Name, xmlDocFile);
            }
        }
    }
}
