using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace McpGateway.Core.Validation;

/// <summary>
/// Interface for asynchronous startup validators.
/// </summary>
public interface IAsyncStartupValidator
{
    /// <summary>
    /// Validates the application startup asynchronously and returns any errors found.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of validation errors. Empty list if validation passes.</returns>
    Task<IReadOnlyList<ValidationError>> ValidateAsync(CancellationToken cancellationToken = default);
}
