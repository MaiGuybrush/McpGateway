using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Text.Json;
using McpGateway.Core.Configuration;

namespace McpGateway.Core.Auth;

/// <summary>
/// Represents a JSON Web Key from a JWKS endpoint.
/// </summary>
public class Jwk
{
    public string? Kid { get; set; }
    public string? Kty { get; set; }
    public string? Alg { get; set; }
    public string? Use { get; set; }
    public string? N { get; set; }
    public string? E { get; set; }
}

/// <summary>
/// Represents a JSON Web Key Set (JWKS).
/// </summary>
public class Jwks
{
    public List<Jwk> Keys { get; set; } = new();
}

/// <summary>
/// Provides public keys from a JWKS endpoint for JWT signature validation with graceful degradation.
/// </summary>
public class JwksPublicKeyProvider
{
    private readonly IMemoryCache _cache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<JwksPublicKeyProvider> _logger;
    private readonly AuthOptions _authOptions;
    private bool _jwksFetchFailedLogged = false;

    /// <summary>
    /// Initializes a new instance of the JwksPublicKeyProvider.
    /// </summary>
    /// <param name="cache">The memory cache for caching JWKS keys.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="options">The authentication options.</param>
    public JwksPublicKeyProvider(
        IMemoryCache cache,
        IHttpClientFactory httpClientFactory,
        ILogger<JwksPublicKeyProvider> logger,
        IOptions<McpGatewayOptions> options)
    {
        _cache = cache;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _authOptions = options.Value.Auth ?? new AuthOptions();
    }

    /// <summary>
    /// Gets the public key for the specified key ID (kid) with graceful degradation.
    /// </summary>
    /// <param name="kid">The key ID.</param>
    /// <param name="correlationId">The correlation ID for logging.</param>
    /// <returns>The RSA public key.</returns>
    /// <exception cref="HttpRequestException">Thrown when JWKS service is unavailable and no cached keys exist.</exception>
    public async Task<RSA?> GetPublicKeyAsync(string kid, string correlationId)
    {
        if (string.IsNullOrEmpty(_authOptions.JwksEndpoint))
        {
            _logger.LogWarning("CorrelationId: {CorrelationId} - JWKS endpoint is not configured", correlationId);
            return null;
        }

        var cacheKey = $"jwks_{_authOptions.JwksEndpoint}_{kid}";
        var jwksCacheKey = $"jwks_cache_{_authOptions.JwksEndpoint}";
        
        // Try to get from cache first
        if (_cache.TryGetValue(cacheKey, out RSA? cachedKey))
        {
            _logger.LogDebug("CorrelationId: {CorrelationId} - JWKS cache hit for kid: {Kid}", correlationId, kid);
            return cachedKey;
        }

        try
        {
            // Fetch JWKS from endpoint
            var jwks = await FetchJwksAsync(correlationId);
            var key = jwks.Keys.FirstOrDefault(k => k.Kid == kid);

            if (key == null)
            {
                _logger.LogWarning("CorrelationId: {CorrelationId} - No key found with kid: {Kid}", correlationId, kid);
                return null;
            }

            // Convert JWK to RSA key
            var rsaKey = ConvertJwkToRsa(key);

            // Cache the key with TTL
            if (rsaKey != null)
            {
                var cacheHours = _authOptions.JwksCacheHours > 0 ? _authOptions.JwksCacheHours : 24;
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromHours(cacheHours));
                
                _cache.Set(cacheKey, rsaKey, cacheOptions);
                
                // Reset failure flag on successful fetch
                _jwksFetchFailedLogged = false;
            }

            return rsaKey;
        }
        catch (RedisConnectionException redisEx)
        {
            // Redis connection failure - use memory cache exclusively
            _logger.LogWarning(redisEx, "CorrelationId: {CorrelationId} - Redis connection failed, using in-memory cache only", correlationId);
            
            // Try to use cached JWKS even if expired
            var cachedJwks = _cache.Get<Jwks?>(jwksCacheKey);
            if (cachedJwks != null)
            {
                _logger.LogWarning("CorrelationId: {CorrelationId} - Using cached JWKS due to Redis failure", correlationId);
                var key = cachedJwks.Keys.FirstOrDefault(k => k.Kid == kid);
                if (key != null)
                {
                    return ConvertJwkToRsa(key);
                }
            }
            
            _logger.LogError("CorrelationId: {CorrelationId} - No cached JWKS keys available during Redis outage", correlationId);
            throw new HttpRequestException("Authentication service temporarily unavailable due to cache failure", redisEx, HttpStatusCode.ServiceUnavailable);
        }
        catch (HttpRequestException httpEx) when (httpEx.StatusCode == HttpStatusCode.RequestTimeout)
        {
            // JWKS service timeout - try cache
            _logger.LogWarning(httpEx, "CorrelationId: {CorrelationId} - JWKS service timeout, attempting cache fallback", correlationId);
            
            var cachedJwks = _cache.Get<Jwks?>(jwksCacheKey);
            if (cachedJwks != null)
            {
                _logger.LogWarning("CorrelationId: {CorrelationId} - Using expired JWKS cache due to service timeout", correlationId);
                var key = cachedJwks.Keys.FirstOrDefault(k => k.Kid == kid);
                if (key != null)
                {
                    return ConvertJwkToRsa(key);
                }
            }
            
            _logger.LogError("CorrelationId: {CorrelationId} - JWKS service timeout and no cached keys available", correlationId);
            throw new HttpRequestException("Authentication service timeout - cached keys unavailable", httpEx, HttpStatusCode.ServiceUnavailable);
        }
        catch (HttpRequestException httpEx)
        {
            // JWKS service failure - try cache
            if (!_jwksFetchFailedLogged)
            {
                _logger.LogWarning(httpEx, "CorrelationId: {CorrelationId} - JWKS service failure, attempting cache fallback", correlationId);
                _jwksFetchFailedLogged = true;
            }
            
            var cachedJwks = _cache.Get<Jwks?>(jwksCacheKey);
            if (cachedJwks != null)
            {
                _logger.LogWarning("CorrelationId: {CorrelationId} - Using cached JWKS due to service failure", correlationId);
                var key = cachedJwks.Keys.FirstOrDefault(k => k.Kid == kid);
                if (key != null)
                {
                    return ConvertJwkToRsa(key);
                }
            }
            
            _logger.LogError("CorrelationId: {CorrelationId} - JWKS service failure and no cached keys available", correlationId);
            throw new HttpRequestException("Authentication service temporarily unavailable", httpEx, HttpStatusCode.ServiceUnavailable);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CorrelationId: {CorrelationId} - Unexpected error fetching or parsing JWKS from {JwksEndpoint}", 
                correlationId, _authOptions.JwksEndpoint);
            
            // Last resort - try any cached JWKS
            var cachedJwks = _cache.Get<Jwks?>(jwksCacheKey);
            if (cachedJwks != null)
            {
                _logger.LogWarning("CorrelationId: {CorrelationId} - Using cached JWKS due to unexpected error", correlationId);
                var key = cachedJwks.Keys.FirstOrDefault(k => k.Kid == kid);
                if (key != null)
                {
                    return ConvertJwkToRsa(key);
                }
            }
            
            throw new HttpRequestException("Authentication service error", ex, HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <summary>
    /// Fetches JWKS from the endpoint with timeout handling.
    /// </summary>
    private async Task<Jwks> FetchJwksAsync(string correlationId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30); // Prevent indefinite hangs
            
            _logger.LogDebug("CorrelationId: {CorrelationId} - Fetching JWKS from {JwksEndpoint}", 
                correlationId, _authOptions.JwksEndpoint);
            
            var response = await client.GetStringAsync(_authOptions.JwksEndpoint);
            
            var jwks = JsonSerializer.Deserialize<Jwks>(response) ?? throw new InvalidOperationException("Failed to deserialize JWKS");
            
            // Cache the full JWKS response
            if (!string.IsNullOrEmpty(_authOptions.JwksEndpoint))
            {
                var cacheHours = _authOptions.JwksCacheHours > 0 ? _authOptions.JwksCacheHours : 24;
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromHours(cacheHours));
                
                _cache.Set("jwks_cache_" + _authOptions.JwksEndpoint, jwks, cacheOptions);
            }

            _logger.LogDebug("CorrelationId: {CorrelationId} - Successfully fetched JWKS with {KeyCount} keys", 
                correlationId, jwks.Keys.Count);
            
            return jwks;
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            // Handle timeout specifically
            throw new HttpRequestException("JWKS request timed out", ex, HttpStatusCode.RequestTimeout);
        }
    }

    /// <summary>
    /// Converts a JWK to RSA public key.
    /// </summary>
    private RSA? ConvertJwkToRsa(Jwk jwk)
    {
        try
        {
            if (jwk.Kty != "RSA" || string.IsNullOrEmpty(jwk.N) || string.IsNullOrEmpty(jwk.E))
            {
                _logger.LogWarning("Invalid RSA JWK: missing required parameters");
                return null;
            }

            var rsa = RSA.Create();
            var parameters = new RSAParameters
            {
                Modulus = Base64UrlDecode(jwk.N),
                Exponent = Base64UrlDecode(jwk.E)
            };

            rsa.ImportParameters(parameters);
            return rsa;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert JWK to RSA key");
            return null;
        }
    }

    /// <summary>
    /// Decodes base64url-encoded string.
    /// </summary>
    private byte[] Base64UrlDecode(string input)
    {
        try
        {
            var output = input;
            output = output.Replace('-', '+');
            output = output.Replace('_', '/');

            switch (output.Length % 4)
            {
                case 0: break;
                case 2: output += "=="; break;
                case 3: output += "="; break;
                default: throw new ArgumentException("Invalid base64url input");
            }

            return Convert.FromBase64String(output);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decode base64url string");
            throw;
        }
    }
}

/// <summary>
/// Custom exception for Redis connection failures.
/// </summary>
public class RedisConnectionException : Exception
{
    public RedisConnectionException(string message, Exception innerException) 
        : base(message, innerException) { }
}