using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
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
/// Provides public keys from a JWKS endpoint for JWT signature validation.
/// </summary>
public class JwksPublicKeyProvider
{
    private readonly IMemoryCache _cache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<JwksPublicKeyProvider> _logger;
    private readonly AuthOptions _authOptions;

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
    /// Gets the public key for the specified key ID (kid).
    /// </summary>
    /// <param name="kid">The key ID.</param>
    /// <returns>The RSA public key.</returns>
    public async Task<RSA?> GetPublicKeyAsync(string kid)
    {
        if (string.IsNullOrEmpty(_authOptions.JwksEndpoint))
        {
            _logger.LogWarning("JWKS endpoint is not configured");
            return null;
        }

        var cacheKey = $"jwks_{_authOptions.JwksEndpoint}_{kid}";
        
        // Try to get from cache
        if (_cache.TryGetValue(cacheKey, out RSA? cachedKey))
        {
            return cachedKey;
        }

        try
        {
            // Fetch JWKS from endpoint
            var jwks = await FetchJwksAsync();
            var key = jwks.Keys.FirstOrDefault(k => k.Kid == kid);

            if (key == null)
            {
                _logger.LogWarning("No key found with kid: {Kid}", kid);
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
            }

            return rsaKey;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch or parse JWKS from {JwksEndpoint}", _authOptions.JwksEndpoint);
            
            // Try to use cached data even if expired (degradation)
            var cachedJwks = _cache.Get<Jwks?>("jwks_cache_" + _authOptions.JwksEndpoint);
            if (cachedJwks != null)
            {
                _logger.LogWarning("Using expired JWKS cache due to fetch failure");
                var key = cachedJwks.Keys.FirstOrDefault(k => k.Kid == kid);
                if (key != null)
                {
                    return ConvertJwkToRsa(key);
                }
            }

            return null;
        }
    }

    private async Task<Jwks> FetchJwksAsync()
    {
        var client = _httpClientFactory.CreateClient();
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

        return jwks;
    }

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

    private byte[] Base64UrlDecode(string input)
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
}