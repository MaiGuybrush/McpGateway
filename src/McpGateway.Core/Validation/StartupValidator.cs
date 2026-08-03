using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace McpGateway.Core.Validation;

/// <summary>
/// Validates gateway configuration at startup per ADR-004.
/// Ensures department prefix uniqueness and tool registry integrity.
/// </summary>
public class StartupValidator
{
    private readonly ILogger<StartupValidator> _logger;

    public StartupValidator(ILogger<StartupValidator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validates the gateway configuration.
    /// </summary>
    /// <param name="configuration">The configuration to validate.</param>
    /// <returns>Validation result with any errors.</returns>
    public ValidationResult Validate(IConfiguration configuration)
    {
        // TODO: Implement ADR-004 validation rules
        // - Department prefix uniqueness
        // - Tool name uniqueness within department
        // - Downstream service reachability

        _logger.LogInformation("Running startup validation");
        return new ValidationResult { IsValid = true };
    }
}

/// <summary>
/// Result of startup validation.
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
}
