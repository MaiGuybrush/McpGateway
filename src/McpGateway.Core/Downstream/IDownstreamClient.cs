namespace McpGateway.Core.Downstream;

/// <summary>
/// Interface for making downstream API calls with automatic identity header injection and retry logic.
/// </summary>
public interface IDownstreamClient
{
    /// <summary>
    /// Makes a GET request to the downstream API.
    /// </summary>
    /// <typeparam name="T">The response type.</typeparam>
    /// <param name="path">The API path.</param>
    /// <param name="query">Optional query parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The deserialized response.</returns>
    Task<T> GetAsync<T>(string path, object? query = null, CancellationToken ct = default);

    /// <summary>
    /// Makes a POST request to the downstream API.
    /// </summary>
    /// <typeparam name="T">The response type.</typeparam>
    /// <param name="path">The API path.</param>
    /// <param name="body">The request body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The deserialized response.</returns>
    Task<T> PostAsync<T>(string path, object body, CancellationToken ct = default);

    /// <summary>
    /// Makes a PUT request to the downstream API.
    /// </summary>
    /// <typeparam name="T">The response type.</typeparam>
    /// <param name="path">The API path.</param>
    /// <param name="body">The request body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The deserialized response.</returns>
    Task<T> PutAsync<T>(string path, object body, CancellationToken ct = default);

    /// <summary>
    /// Makes a DELETE request to the downstream API.
    /// </summary>
    /// <param name="path">The API path.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteAsync(string path, CancellationToken ct = default);
}