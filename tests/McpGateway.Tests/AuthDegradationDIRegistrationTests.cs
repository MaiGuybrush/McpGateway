using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using McpGateway.Core.Cache;
using McpGateway.Core.Configuration;
using McpGateway.Core.Hosting;
using Xunit;
using StackExchange.Redis;

namespace McpGateway.Tests;

/// <summary>
/// Tests for DI registration with Redis degradation scenarios.
/// </summary>
public class AuthDegradationDIRegistrationTests
{
    [Fact]
    public void AddMcpGateway_WhenRedisConnectionFails_ShouldRegisterNullTokenCache()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new System.Collections.Generic.KeyValuePair<string, string>("McpGateway:Department", "D6"),
                new System.Collections.Generic.KeyValuePair<string, string>("McpGateway:RoutePrefix", "/D6"),
                new System.Collections.Generic.KeyValuePair<string, string>("McpGateway:TokenCache:ConnectionString", "invalid-redis:6379"),
                new System.Collections.Generic.KeyValuePair<string, string>("McpGateway:TokenCache:Type", "Redis")
            })
            .Build();
        
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions<McpGatewayOptions>()
            .Bind(configuration.GetSection("McpGateway"))
            .ValidateDataAnnotations();

        // Act
        services.AddMcpGateway();
        var provider = services.BuildServiceProvider();
        
        // _redis_ should be null or throw when accessed
        var redis = provider.GetService<IConnectionMultiplexer>();
        redis.Should().BeNull();
        
        // TokenCacheService should still be registered (as NullTokenCache)
        var cacheService = provider.GetRequiredService<ITokenCacheService>();
        cacheService.Should().BeAssignableTo<NullTokenCacheService>();
    }

    [Fact]
    public void AddMcpGateway_WhenNoRedisConfigured_ShouldRegisterNullTokenCacheWithMemory()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new System.Collections.Generic.KeyValuePair<string, string>("McpGateway:Department", "D6"),
                new System.Collections.Generic.KeyValuePair<string, string>("McpGateway:RoutePrefix", "/D6")
            })
            .Build();
        
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions<McpGatewayOptions>()
            .Bind(configuration.GetSection("McpGateway"))
            .ValidateDataAnnotations();

        // Act
        services.AddMcpGateway();
        var provider = services.BuildServiceProvider();
        
        // TokenCacheService should be registered as NullTokenCache
        var cacheService = provider.GetRequiredService<ITokenCacheService>();
        cacheService.Should().BeAssignableTo<NullTokenCacheService>();
        
        // Memory cache should be available
        var memoryCache = provider.GetService<IMemoryCache>();
        memoryCache.Should().NotBeNull();
    }

    [Fact]
    public async Task NullTokenCacheService_ShouldFallbackToDictionary_WhenMemoryCacheUnavailable()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var cacheService = new NullTokenCacheService(
            loggerFactory.CreateLogger<NullTokenCacheService>(), 
            null); // No IMemoryCache

        var context = new ToolContext("test-user", "D6", "Admin", "API-KEY", "test-agent", "test-correlation");
        
        // Act
        await cacheService.SetAsync("API-KEY", "test-token", context, 5);
        var result = await cacheService.GetAsync("API-KEY", "test-token");
        
        // Assert
        result.Should().NotBeNull();
        result!.UserId.Should().Be("test-user");
    }

    [Fact]
    public async Task NullTokenCacheService_ShouldUseMemoryCache_WhenAvailable()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        
        var cacheService = new NullTokenCacheService(
            loggerFactory.CreateLogger<NullTokenCacheService>(),
            memoryCache);

        var context = new ToolContext("test-user", "D6", "Admin", "API-KEY", "test-agent", "test-correlation");
        
        // Act
        await cacheService.SetAsync("API-KEY", "test-token", context, 5);
        var result = await cacheService.GetAsync("API-KEY", "test-token");
        
        // Assert
        result.Should().NotBeNull();
        result!.UserId.Should().Be("test-user");
        
        // Verify it's in memory cache
        var cacheKey = "auth:API-KEY:J+Z00Kv8BE6JjOfI23uAEdLmugI=";
        memoryCache.TryGetValue(cacheKey, out ToolContext cached).Should().BeTrue();
        cached.Should().NotBeNull();
    }

    [Fact]
    public async Task RedisTokenCacheService_ShouldReturnNullOnConnectionFailure()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        
        // Create a mock/disconnected Redis multiplexer
        var redis = ConnectionMultiplexer.Connect("localhost:6379,connectTimeout=1");
        
        var cacheService = new RedisTokenCacheService(
            redis,
            loggerFactory.CreateLogger<RedisTokenCacheService>());

        // Act
        var result = await cacheService.GetAsync("API-KEY", "test-token");
        
        // Assert
        result.Should().BeNull(); // Should handle gracefully and return null
    }
}