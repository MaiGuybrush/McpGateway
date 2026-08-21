using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using McpGateway.Core.Configuration;

namespace McpGateway.Core.Validation;

/// <summary>
/// Validates during startup that the gateway's system name is registered in UAC API.
/// </summary>
public class UacSystemStartupValidator : IAsyncStartupValidator
{
    private readonly IApiKeyValidator _apiKeyValidator;
    private readonly McpGatewayOptions _gatewayOptions;
    private readonly AuthOptions _authOptions;
    private readonly ILogger<UacSystemStartupValidator> _logger;

    public UacSystemStartupValidator(
        IApiKeyValidator apiKeyValidator,
        IOptions<McpGatewayOptions> options,
        ILogger<UacSystemStartupValidator> logger)
    {
        _apiKeyValidator = apiKeyValidator;
        _gatewayOptions = options.Value;
        _authOptions = options.Value.Auth ?? new AuthOptions();
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ValidationError>> ValidateAsync(CancellationToken cancellationToken = default)
    {
        if (!_authOptions.Enabled)
        {
            _logger.LogInformation("UAC Authentication is disabled; skipping system registration validation");
            return Array.Empty<ValidationError>();
        }

        var systemName = !string.IsNullOrWhiteSpace(_authOptions.SystemName)
            ? _authOptions.SystemName
            : _gatewayOptions.Department;

        _logger.LogInformation("Validating UAC system registration for '{SystemName}'...", systemName);

        var isRegistered = await _apiKeyValidator.ValidateSystemRegisteredAsync(cancellationToken);
        if (!isRegistered)
        {
            var error = new ValidationError
            {
                ToolName = "UacAuth",
                FileLocation = "UAC API",
                Description = $"System '{systemName}' is not registered in UAC API or all UAC API nodes were unreachable."
            };
            return new List<ValidationError> { error };
        }

        _logger.LogInformation("UAC system registration verified for '{SystemName}'", systemName);
        return Array.Empty<ValidationError>();
    }
}
