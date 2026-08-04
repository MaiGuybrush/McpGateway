using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using McpGateway.Core.Configuration;
using McpGateway.Core.Tools;
using McpGateway.Core.Observability;

namespace McpGateway.Core.Downstream;

/// <summary>
/// Downstream client implementation with automatic identity header injection and retry logic.
/// </summary>
public class DownstreamClient : IDownstreamClient
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<OcelotOptions> _ocelotOptions;
    private readonly ToolContext? _toolContext;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ICorrelationIdService? _correlationIdService;

    /// <summary>
    /// Initializes a new instance of the DownstreamClient.
    /// </summary>
    /// <param name="httpClient">The named HttpClient instance.</param>
    /// <param name="ocelotOptions">Ocelot configuration options.</param>
    /// <param name="toolContext">Optional tool context for header injection.</param>
    /// <param name="correlationIdService">Optional correlation ID service for request tracking.</param>
    public DownstreamClient(
        HttpClient httpClient,
        IOptions<OcelotOptions> ocelotOptions,
        ToolContext? toolContext = null,
        ICorrelationIdService? correlationIdService = null)
    {
        _httpClient = httpClient;
        _ocelotOptions = ocelotOptions;
        _toolContext = toolContext;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        _correlationIdService = correlationIdService;
        
        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        if (_ocelotOptions.Value.TimeoutSeconds > 0)
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(_ocelotOptions.Value.TimeoutSeconds);
        }
    }

    /// <inheritdoc />
    public async Task<T> GetAsync<T>(string path, object? query = null, CancellationToken ct = default)
    {
        var url = BuildUrl(path, query);
        return await ExecuteWithRetryAsync(
            async () =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                InjectHeaders(request);
                
                using var response = await _httpClient.SendAsync(request, ct);
                return await HandleResponseAsync<T>(response);
            },
            canRetry: true,
            ct);
    }

    /// <inheritdoc />
    public async Task<T> PostAsync<T>(string path, object body, CancellationToken ct = default)
    {
        var url = BuildUrl(path);
        return await ExecuteAsync(
            async () =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                InjectHeaders(request);
                request.Content = JsonContent.Create(body, options: _jsonOptions);
                
                using var response = await _httpClient.SendAsync(request, ct);
                return await HandleResponseAsync<T>(response);
            },
            canRetry: false,
            ct);
    }

    /// <inheritdoc />
    public async Task<T> PutAsync<T>(string path, object body, CancellationToken ct = default)
    {
        var url = BuildUrl(path);
        return await ExecuteWithRetryAsync(
            async () =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Put, url);
                InjectHeaders(request);
                request.Content = JsonContent.Create(body, options: _jsonOptions);
                
                using var response = await _httpClient.SendAsync(request, ct);
                return await HandleResponseAsync<T>(response);
            },
            canRetry: true,
            ct);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string path, CancellationToken ct = default)
    {
        var url = BuildUrl(path);
        await ExecuteWithRetryAsync(
            async () =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Delete, url);
                InjectHeaders(request);
                
                using var response = await _httpClient.SendAsync(request, ct);
                await HandleResponseAsync<object>(response);
                return (object?)null;
            },
            canRetry: true,
            ct);
    }

    private async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, bool canRetry, CancellationToken ct)
    {
        return await operation();
    }

    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, bool canRetry, CancellationToken ct)
    {
        if (!canRetry || _ocelotOptions.Value.Retry == null || _ocelotOptions.Value.Retry.Count <= 0)
        {
            return await operation();
        }

        var retryCount = _ocelotOptions.Value.Retry.Count;
        var backoffMs = _ocelotOptions.Value.Retry.BackoffMs;
        
        for (var attempt = 0; attempt <= retryCount; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (HttpRequestException ex) when (IsRetryableException(ex))
            {
                if (attempt == retryCount)
                    throw;
                    
                await Task.Delay(backoffMs * (attempt + 1), ct);
            }
            catch (Exception) when (!IsRetryableStatusCode(200)) // Will be handled in HandleResponseAsync
            {
                if (attempt == retryCount)
                    throw;
                    
                await Task.Delay(backoffMs * (attempt + 1), ct);
            }
        }

        throw new InvalidOperationException("Retry logic error");
    }

    private void InjectHeaders(HttpRequestMessage request)
    {
        if (_toolContext == null) return;

        request.Headers.TryAddWithoutValidation("X-User-Id", _toolContext.UserId);
        request.Headers.TryAddWithoutValidation("X-User-Department", _toolContext.Department);
        request.Headers.TryAddWithoutValidation("X-User-Role", _toolContext.Role);
        request.Headers.TryAddWithoutValidation("X-Auth-Type", _toolContext.TokenType);
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", _toolContext.CorrelationId);
        request.Headers.TryAddWithoutValidation("X-Gateway-Department", _toolContext.Department);
    }

    private string BuildUrl(string path, object? query = null)
    {
        var baseUrl = _ocelotOptions.Value.BaseUrl?.TrimEnd('/');
        var normalizedPath = path.TrimStart('/');
        var url = $"{baseUrl}/{normalizedPath}";

        if (query != null)
        {
            var queryString = System.Web.HttpUtility.ParseQueryString(string.Empty);
            foreach (var prop in query.GetType().GetProperties())
            {
                var value = prop.GetValue(query);
                if (value != null)
                {
                    queryString[prop.Name] = value.ToString();
                }
            }
            url += "?" + queryString.ToString();
        }

        return url;
    }

    private async Task<T> HandleResponseAsync<T>(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            if (typeof(T) == typeof(object) || typeof(T) == typeof(string))
            {
                return default!;
            }
            
            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(content, _jsonOptions) 
                ?? throw new InvalidOperationException("Failed to deserialize response");
        }

        var statusCode = (int)response.StatusCode;
        if (!IsRetryableStatusCode(statusCode))
        {
            throw new HttpRequestException(
                $"Request failed with status {(int)response.StatusCode}: {response.ReasonPhrase}");
        }

        throw new HttpRequestException(
            $"Request failed with retryable status {(int)response.StatusCode}: {response.ReasonPhrase}");
    }

    private static bool IsRetryableException(HttpRequestException ex)
    {
        // Network errors are always retryable
        return true;
    }

    private static bool IsRetryableStatusCode(int statusCode)
    {
        return statusCode == 408 || // Request Timeout
               statusCode == 429 || // Too Many Requests
               (statusCode >= 500 && statusCode < 600); // Server errors
    }
}