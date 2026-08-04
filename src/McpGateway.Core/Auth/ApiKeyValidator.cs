using System;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using McpGateway.Core.Cache;
using McpGateway.Core.Configuration;
using McpGateway.Core.Tools;
using McpGateway.Core.Validation;
using StackExchange.Redis;

namespace McpGateway.Core.Auth;

/// <summary>
/// API-KEY validation request model.
/// </summary>
public class ApiKeyValidationRequest
{
    [JsonPropertyName("apiKey")]
    public string ApiKey { get; set; } = string.Empty;
}

/// <summary>
/// API-KEY validation response model.
/// </summary>
public class ApiKeyValidationResponse
{
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("department")]
    public string Department { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("agentId")]
    public string AgentId { get; set; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; set; }
}

/// <summary>
/// Validates API-KEY tokens by calling the API-KEY validation service with graceful degradation.
/// </summary>
public class ApiKeyValidator : IApiKeyValidator
{
    private readonly HttpClient _httpClient;
    private readonly ITokenCacheService _cacheService;
    private readonly ILogger<ApiKeyValidator> _logger;
    private readonly AuthOptions _authOptions;
    private readonly TokenCacheOptions _cacheOptions;
    private bool _serviceFailureLogged = false;

    public ApiKeyValidator(
        HttpClient httpClient,
        ITokenCacheService cacheService,
        ILogger<ApiKeyValidator> logger,
        IOptions<McpGatewayOptions> options)
    {
        _httpClient = httpClient;
        _cacheService = cacheService;
        _logger = logger;
        _authOptions = options.Value.Auth ?? new AuthOptions();
        _cacheOptions = options.Value.TokenCache ?? new TokenCacheOptions();
    }

    /// <summary>
    /// Validates an API-KEY with graceful degradation.
    /// </summary>
    /// <param name="apiKey">The API key to validate.</param>
    /// <param name="correlationId">The correlation ID for logging.</param>
    /// <returns>Tool context if validation succeeds, null if unauthorized, throws if service unavailable.</returns>
    /// <exception cref="HttpRequestException">Thrown when validation service is unavailable and no cached data exists.</exception>
        public async Task<ToolContext?> ValidateAsync(string apiKey, string correlationId)
    {
        var tokenHash = GetTokenHash(apiKey);
        
        try
        {
            // First check cache
            var contextFromCache = await _cacheService.GetAsync("API-KEY", apiKey);
            if (contextFromCache != null)
            {
                _logger.LogDebug("CorrelationId: {CorrelationId} - API-KEY cache hit for token {TokenHash}", 
                    correlationId, tokenHash);
                return contextFromCache;
            }

            _logger.LogDebug("CorrelationId: {CorrelationId} - API-KEY cache miss for token {TokenHash}", 
                correlationId, tokenHash);

            // Call API-KEY validation service
            var request = new ApiKeyValidationRequest { ApiKey = apiKey };
            var requestBody = JsonSerializer.Serialize(request);
            var content = new StringContent(requestBody, Encoding.UTF8, "application/json");

            var timeoutSeconds = _authOptions.ApiKeyTimeoutSeconds > 0 
                ? _authOptions.ApiKeyTimeoutSeconds 
                : 3; // Default 3 seconds

            _logger.LogDebug("CorrelationId: {CorrelationId} - Calling API-KEY validation service with {Timeout}s timeout", 
                correlationId, timeoutSeconds);
            
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            
            var response = await _httpClient.PostAsync("validate", content, cts.Token);
            
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning("CorrelationId: {CorrelationId} - API-KEY validation failed: unauthorized token", correlationId);
                return null;
            }

            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            var validationResult = JsonSerializer.Deserialize<ApiKeyValidationResponse>(responseBody);

            if (validationResult == null || !validationResult.Success)
            {
                _logger.LogWarning("CorrelationId: {CorrelationId} - API-KEY validation failed: invalid response", correlationId);
                return null;
            }

            // Create tool context
            var toolContext = new ToolContext(
                validationResult.UserId,
                validationResult.Department,
                validationResult.Role,
                "API-KEY",
                validationResult.AgentId,
                correlationId
            );

            // Cache the result
            var ttlMinutes = _cacheOptions.ApiKeyTtlMinutes > 0 
                ? _cacheOptions.ApiKeyTtlMinutes 
                : 5; // Default 5 minutes

            await _cacheService.SetAsync("API-KEY", apiKey, toolContext, ttlMinutes);

            _logger.LogInformation("CorrelationId: {CorrelationId} - API-KEY validation successful for user {UserId}", 
                correlationId, toolContext.UserId);
            
            // Reset failure flag on successful validation
            _serviceFailureLogged = false;
            
            return toolContext;
        }
        catch (TaskCanceledException ex) // Timeout
        {
            _logger.LogWarning(ex, "CorrelationId: {CorrelationId} - API-KEY validation service timeout for token {TokenHash}", 
                correlationId, tokenHash);
            
            // Try cached data even if expired
            var cachedContext = await TryGetCachedContextAsync("API-KEY", apiKey);
            if (cachedContext != null)
            {
                _logger.LogWarning("CorrelationId: {CorrelationId} - Using cached API-KEY validation due to service timeout", 
                    correlationId);
                return cachedContext;
            }
            
            _logger.LogError("CorrelationId: {CorrelationId} - API-KEY service timeout and no cached validation available", 
                correlationId);
            throw new HttpRequestException("API-KEY validation service timeout and cached data unavailable", ex, HttpStatusCode.ServiceUnavailable);
        }
        catch (HttpRequestException ex)
        {
            // Handle Redis connection failure
            if (ex.InnerException is RedisConnectionException || ex.Message.Contains("Redis"))
            {
                _logger.LogWarning(ex, "CorrelationId: {CorrelationId} - Redis connection failed during API-KEY validation", 
                    correlationId);
                
                // Try cached data
                var cachedValidationResult = await TryGetCachedContextAsync("API-KEY", apiKey);
                if (cachedValidationResult != null)
                {
                    _logger.LogWarning("CorrelationId: {CorrelationId} - Using cached API-KEY validation due to Redis failure", 
                        correlationId);
                    return cachedValidationResult;
                }
                
                _logger.LogError("CorrelationId: {CorrelationId} - Redis failure and no cached validation available", 
                    correlationId);
                throw new HttpRequestException("Authentication service temporarily unavailable due to cache failure", ex, HttpStatusCode.ServiceUnavailable);
            }
            
            // Handle other HTTP errors
            if (!_serviceFailureLogged)
            {
                _logger.LogWarning(ex, "CorrelationId: {CorrelationId} - API-KEY validation service error, attempting cache fallback", 
                    correlationId);
                _serviceFailureLogged = true;
            }
            
            // Try cached data
            var cachedContext = await TryGetCachedContextAsync("API-KEY", apiKey);
            if (cachedContext != null)
            {
                _logger.LogWarning("CorrelationId: {CorrelationId} - Using cached API-KEY validation due to service failure", 
                    correlationId);
                return cachedContext;
            }
            
            _logger.LogError("CorrelationId: {CorrelationId} - API-KEY validation service error and no cached data available", 
                correlationId);
            throw new HttpRequestException("API-KEY validation service temporarily unavailable", ex, HttpStatusCode.ServiceUnavailable);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CorrelationId: {CorrelationId} - Unexpected error during API-KEY validation", correlationId);
            
            // Last resort - try any cached data
            var cachedContext = await TryGetCachedContextAsync("API-KEY", apiKey);
            if (cachedContext != null)
            {
                _logger.LogWarning("CorrelationId: {CorrelationId} - Using cached API-KEY validation due to unexpected error", 
                    correlationId);
                return cachedContext;
            }
            
            throw new HttpRequestException("Authentication service error", ex, HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <summary>
    /// Attempts to retrieve cached context even if expired.
    /// </summary>
    private async Task<ToolContext?> TryGetCachedContextAsync(string tokenType, string token)
    {
        try
        {
            // First try normal cache lookup
            var cached = await _cacheService.GetAsync(tokenType, token);
            if (cached != null)
            {
                return cached;
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve cached validation result during degradation");
            return null;
        }
    }

    private string GetTokenHash(string token)
    {
        try
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
            return Convert.ToBase64String(hash)[..10];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to compute token hash");
            return "unknown";
        }
    }
}