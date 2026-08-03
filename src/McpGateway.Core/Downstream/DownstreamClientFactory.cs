using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace McpGateway.Core.Downstream;

/// <summary>
/// Factory for creating HTTP clients configured for downstream API calls.
/// Integrates with Ocelot gateway for service discovery.
/// </summary>
public class DownstreamClientFactory
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DownstreamClientFactory> _logger;

    public DownstreamClientFactory(IConfiguration configuration, ILogger<DownstreamClientFactory> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Creates an HTTP client for calling a downstream service.
    /// </summary>
    /// <param name="serviceName">The downstream service name.</param>
    /// <returns>An HTTP client configured for the service.</returns>
    public HttpClient CreateClient(string serviceName)
    {
        // TODO: Configure HttpClient with Ocelot gateway, auth headers, timeouts
        _logger.LogDebug("Creating downstream client for {ServiceName}", serviceName);
        return new HttpClient();
    }
}
