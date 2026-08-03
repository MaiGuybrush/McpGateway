using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;

namespace McpGateway.Core.Configuration;

/// <summary>
/// Validates McpGatewayOptions configuration.
/// </summary>
public class McpGatewayOptionsValidator : IValidateOptions<McpGatewayOptions>
{
    private readonly ILogger<McpGatewayOptionsValidator> _logger;

    public McpGatewayOptionsValidator(ILogger<McpGatewayOptionsValidator> logger)
    {
        _logger = logger;
    }

    public ValidateOptionsResult Validate(string? name, McpGatewayOptions options)
    {
        // Check required fields
        if (string.IsNullOrEmpty(options.Department))
        {
            throw new ConfigurationException("Department is required");
        }

        if (string.IsNullOrEmpty(options.RoutePrefix))
        {
            throw new ConfigurationException("RoutePrefix is required");
        }

        // Warning if RoutePrefix doesn't match /{Department}
        var expectedRoutePrefix = $"/" + options.Department.ToLowerInvariant();
        if (!options.RoutePrefix.Equals(expectedRoutePrefix, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("RoutePrefix '{RoutePrefix}' does not match recommended format '{ExpectedRoutePrefix}'. Consider using '/{Department}' for consistency", 
                options.RoutePrefix, expectedRoutePrefix, options.Department);
        }

        return ValidateOptionsResult.Success;
    }
}

/// <summary>
/// Exception thrown when configuration is invalid.
/// </summary>
public class ConfigurationException : Exception
{
    public ConfigurationException(string message) : base(message) { }
}