using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace McpGateway.Core.Auth;

/// <summary>
/// Resolves UAC API endpoints dynamically from Consul K/V with fallback support.
/// </summary>
public interface IUacApiEndpointResolver
{
    /// <summary>
    /// Resolves the list of available UAC API endpoint URLs.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A read-only list of UAC API endpoint base URLs.</returns>
    Task<IReadOnlyList<string>> ResolveEndpointsAsync(CancellationToken cancellationToken = default);
}
