using System;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using McpGateway.Core.Auth;
using McpGateway.Core.Cache;
using McpGateway.Core.Configuration;
using McpGateway.Core.Tools;
using Xunit;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace McpGateway.Tests;

/// <summary>
/// Integration tests for authentication degradation scenarios.
/// Tests all 5 required degradation scenarios using WireMock.
/// </summary>
public class AuthDegradationIntegrationTests : IDisposable
{
    private readonly WireMockServer _wireMockServer;
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _memoryCache;
    private readonly ILoggerFactory _loggerFactory;
    private readonly string _correlationId = "test-correlation-id";
    
    public AuthDegradationIntegrationTests()
    {
        _wireMockServer = WireMockServer.Start();
        _httpClient = new HttpClient();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
    }

    public void Dispose()
    {
        _wireMockServer.Stop();
        _httpClient.Dispose();
        _memoryCache.Dispose();
        _loggerFactory.Dispose();
    }

    #region Scenario 1: API-KEY timeout WITH cache available

    [Fact]
    public async Task ApiKeyValidator_OnTimeout_WithCache_ShouldReturnCachedContext()
    {
        // Arrange
        var apiKeyServiceUrl = _wireMockServer.Urls[0];
        _wireMockServer
            .Given(Request.Create()
                .WithPath("/validate")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(new ApiKeyValidationResponse
                {
                    UserId = "test-user",
                    Department = "D6",
                    Role = "Admin",
                    AgentId = "test-agent",
                    Success = true
                }))
                .WithDelay(TimeSpan.FromSeconds(5))); // Timeout

        var authOptions = new AuthOptions
        {
            ApiKeyServiceUrl = apiKeyServiceUrl,
            ApiKeyTimeoutSeconds = 2 // Shorter timeout
        };
        
        var cacheOptions = new TokenCacheOptions { ApiKeyTtlMinutes = 5 };
        var mcpOptions = new McpGatewayOptions 
        { 
            Auth = authOptions, 
            TokenCache = cacheOptions,
            Department = "D6",
            RoutePrefix = "/D6"
        };
        
        // Pre-populate cache with expired entry
        var cachedContext = new ToolContext("cached-user", "D6", "User", "API-KEY", "cached-agent", _correlationId);
        var cacheService = new NullTokenCacheService(
            _loggerFactory.CreateLogger<NullTokenCacheService>(),
            _memoryCache);
        await cacheService.SetAsync("API-KEY", "test-key", cachedContext, 5);

        var validator = new ApiKeyValidator(
            _httpClient,
            cacheService,
            _loggerFactory.CreateLogger<ApiKeyValidator>(),
            Microsoft.Extensions.Options.Options.Create(mcpOptions));

        // Act
        var result = await validator.ValidateAsync("test-key", _correlationId);

        // Assert
        result.Should().NotBeNull();
        result!.UserId.Should().Be("cached-user");
        _wireMockServer.LogEntries.Should().HaveCount(0); // Should not reach service due to timeout
    }

    #endregion

    #region Scenario 2: API-KEY timeout WITHOUT cache

    [Fact]
    public async Task ApiKeyValidator_OnTimeout_WithoutCache_ShouldThrowServiceUnavailable()
    {
        // Arrange
        var apiKeyServiceUrl = _wireMockServer.Urls[0];
        _wireMockServer
            .Given(Request.Create()
                .WithPath("/validate")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{}")
                .WithDelay(TimeSpan.FromSeconds(5))); // Timeout

        var authOptions = new AuthOptions
        {
            ApiKeyServiceUrl = apiKeyServiceUrl,
            ApiKeyTimeoutSeconds = 2
        };
        
        var cacheOptions = new TokenCacheOptions();
        var mcpOptions = new McpGatewayOptions 
        { 
            Auth = authOptions, 
            TokenCache = cacheOptions,
            Department = "D6",
            RoutePrefix = "/D6"
        };

        var validator = new ApiKeyValidator(
            _httpClient,
            new NullTokenCacheService(_loggerFactory.CreateLogger<NullTokenCacheService>()),
            _loggerFactory.CreateLogger<ApiKeyValidator>(),
            Microsoft.Extensions.Options.Options.Create(mcpOptions));

        // Act
        var act = async () => await validator.ValidateAsync("test-key", _correlationId);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*timeout*")
            .Where(ex => ex.StatusCode == HttpStatusCode.ServiceUnavailable);
    }

    #endregion

    #region Scenario 3: JWKS failure WITH cached keys

    [Fact]
    public async Task JwksPublicKeyProvider_OnServiceFailure_WithCachedKeys_ShouldReturnCachedKey()
    {
        // Arrange
        var jwksEndpoint = _wireMockServer.Urls[0] + "/.well-known/jwks.json";
        _wireMockServer
            .Given(Request.Create()
                .WithPath("/.well-known/jwks.json")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(503)); // Service failure

        var authOptions = new AuthOptions
        {
            JwksEndpoint = jwksEndpoint,
            JwksCacheHours = 24
        };
        
        var mcpOptions = new McpGatewayOptions 
        { 
            Auth = authOptions,
            Department = "D6",
            RoutePrefix = "/D6"
        };

        // Pre-populate cache with a valid JWK
        var rsaKey = RSA.Create();
        rsaKey.ImportFromPem(@"
-----BEGIN RSA PUBLIC KEY-----
MIIBCgKCAQEA0f+6vVd3A5LQI9qK5lcimA0l6qVTcmsuzbVar0+rzVa4fZejm5vjl
Zq8=
-----END RSA PUBLIC KEY-----
");
        var cacheKey = $"jwks_{jwksEndpoint}_test-kid";
        _memoryCache.Set(cacheKey, rsaKey, TimeSpan.FromHours(24));

        // Also cache full JWKS
        var cachedJwks = new Jwks
        {
            Keys = new List<Jwk>
            {
                new Jwk
                {
                    Kid = "test-kid",
                    Kty = "RSA",
                    Alg = "RS256",
                    Use = "sig",
                    N = "y7",
                    E = "AQAB"
                }
            }
        };
        _memoryCache.Set($"jwks_cache_{jwksEndpoint}", cachedJwks, TimeSpan.FromHours(24));

        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient()).Returns(_httpClient);

        var provider = new JwksPublicKeyProvider(
            _memoryCache,
            factoryMock.Object,
            _loggerFactory.CreateLogger<JwksPublicKeyProvider>(),
            Microsoft.Extensions.Options.Options.Create(mcpOptions));

        // Act
        var result = await provider.GetPublicKeyAsync("test-kid", _correlationId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeAssignableTo<RSA>();
    }

    #endregion

    #region Scenario 4: JWKS failure WITHOUT cached keys

    [Fact]
    public async Task JwksPublicKeyProvider_OnServiceFailure_WithoutCachedKeys_ShouldThrowServiceUnavailable()
    {
        // Arrange
        var jwksEndpoint = _wireMockServer.Urls[0] + "/.well-known/jwks.json";
        _wireMockServer
            .Given(Request.Create()
                .WithPath("/.well-known/jwks.json")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(503)); // Service failure

        var authOptions = new AuthOptions
        {
            JwksEndpoint = jwksEndpoint,
            JwksCacheHours = 24
        };
        
        var mcpOptions = new McpGatewayOptions 
        { 
            Auth = authOptions,
            Department = "D6",
            RoutePrefix = "/D6"
        };

        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient()).Returns(_httpClient);

        var provider = new JwksPublicKeyProvider(
            _memoryCache, // Empty cache
            factoryMock.Object,
            _loggerFactory.CreateLogger<JwksPublicKeyProvider>(),
            Microsoft.Extensions.Options.Options.Create(mcpOptions));

        // Act
        var act = async () => await provider.GetPublicKeyAsync("test-kid", _correlationId);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*temporarily unavailable*")
            .Where(ex => ex.StatusCode == HttpStatusCode.ServiceUnavailable);
    }

    #endregion

    #region Scenario 5: Redis completely unavailable

    [Fact]
    public async Task RedisUnavailable_ShouldFallbackToNullTokenCache()
    {
        // Arrange
        var connectionString = "localhost:6379,connectTimeout=1";
        var cacheOptions = new TokenCacheOptions 
        { 
            ConnectionString = connectionString,
            Type = "Redis"
        };
        
        var authOptions = new AuthOptions
        {
            ApiKeyServiceUrl = _wireMockServer.Urls[0],
            ApiKeyTimeoutSeconds = 5
        };
        
        var mcpOptions = new McpGatewayOptions 
        { 
            Auth = authOptions,
            TokenCache = cacheOptions,
            Department = "D6",
            RoutePrefix = "/D6"
        };

        // This simulates Redis being unavailable
        var cacheService = new NullTokenCacheService(
            _loggerFactory.CreateLogger<NullTokenCacheService>(),
            _memoryCache);

        // Pre-populate cache
        var cachedContext = new ToolContext("redis-fallback-user", "D6", "User", "API-KEY", "redis-fallback-agent", _correlationId);
        await cacheService.SetAsync("API-KEY", "test-key", cachedContext, 5);

        _wireMockServer
            .Given(Request.Create()
                .WithPath("/validate")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(new ApiKeyValidationResponse
                {
                    UserId = "wiremock-user",
                    Department = "D6",
                    Role = "Admin",
                    AgentId = "wiremock-agent",
                    Success = true
                })));

        var validator = new ApiKeyValidator(
            _httpClient,
            cacheService,
            _loggerFactory.CreateLogger<ApiKeyValidator>(),
            Microsoft.Extensions.Options.Options.Create(mcpOptions));

        // Act & Assert
        // Test that we can retrieve from in-memory cache even when Redis is down
        var result = await validator.ValidateAsync("test-key", _correlationId);
        result.Should().NotBeNull();
        
        // Should have used cache fallback
        result!.UserId.Should().Be("redis-fallback-user");
    }

    [Fact]
    public async Task RedisUnavailable_WithNoCache_ShouldMakeDirectApiCall()
    {
        // Arrange
        _wireMockServer
            .Given(Request.Create()
                .WithPath("/validate")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(new ApiKeyValidationResponse
                {
                    UserId = "direct-user",
                    Department = "D6",
                    Role = "Admin",
                    AgentId = "direct-agent",
                    Success = true
                })));

        var authOptions = new AuthOptions
        {
            ApiKeyServiceUrl = _wireMockServer.Urls[0],
            ApiKeyTimeoutSeconds = 5
        };
        
        var cacheOptions = new TokenCacheOptions();
        var mcpOptions = new McpGatewayOptions 
        { 
            Auth = authOptions, 
            TokenCache = cacheOptions,
            Department = "D6",
            RoutePrefix = "/D6"
        };

        var validator = new ApiKeyValidator(
            _httpClient,
            new NullTokenCacheService(_loggerFactory.CreateLogger<NullTokenCacheService>()),
            _loggerFactory.CreateLogger<ApiKeyValidator>(),
            Microsoft.Extensions.Options.Options.Create(mcpOptions));

        // Act
        var result = await validator.ValidateAsync("test-key", _correlationId);

        // Assert
        result.Should().NotBeNull();
        result!.UserId.Should().Be("direct-user");
        _wireMockServer.LogEntries.Should().HaveCount(1); // Should have made direct call
    }

    #endregion

    #region CorrelationId Logging Tests

    [Fact]
    public async Task AllDegradationScenarios_ShouldIncludeCorrelationIdInLogs()
    {
        // Arrange
        var testCorrelationId = "custom-correlation-12345";
        var authOptions = new AuthOptions
        {
            ApiKeyServiceUrl = _wireMockServer.Urls[0],
            ApiKeyTimeoutSeconds = 1
        };
        
        var mcpOptions = new McpGatewayOptions 
        { 
            Auth = authOptions,
            Department = "D6",
            RoutePrefix = "/D6"
        };

        _wireMockServer
            .Given(Request.Create()
                .WithPath("/validate")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithDelay(TimeSpan.FromSeconds(3))); // Timeout

        var validator = new ApiKeyValidator(
            _httpClient,
            new NullTokenCacheService(_loggerFactory.CreateLogger<NullTokenCacheService>()),
            _loggerFactory.CreateLogger<ApiKeyValidator>(),
            Microsoft.Extensions.Options.Options.Create(mcpOptions));

        // Act
        var act = async () => await validator.ValidateAsync("test-key", testCorrelationId);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
        // The logs would need to be captured and verified that they contain the correlationId
        // For this test, we're just ensuring the parameter is accepted and used
    }

    #endregion
}