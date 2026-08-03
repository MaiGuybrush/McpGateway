using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using McpGateway.Core.Configuration;
using McpGateway.Core.Hosting;
using Moq;
using System;
using Xunit;
using System.Collections.Generic;

namespace McpGateway.Tests.Configuration;

public class McpGatewayOptionsTests
{
    [Fact]
    public void AddMcpGateway_WithMissingDepartment_ShouldThrowConfigurationException()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>()
            {
                {"McpGateway:RoutePrefix", "/test"}
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddMcpGateway();

        // Act & Assert
        var serviceProvider = services.BuildServiceProvider();
        var optionsAccessor = serviceProvider.GetRequiredService<IOptions<McpGatewayOptions>>();
        
        var ex = Assert.Throws<ConfigurationException>(() => optionsAccessor.Value);
        Assert.Equal("Department is required", ex.Message);
    }

    [Fact]
    public void AddMcpGateway_WithMissingRoutePrefix_ShouldThrowConfigurationException()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>()
            {
                {"McpGateway:Department", "report"}
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddMcpGateway();

        // Act & Assert
        var serviceProvider = services.BuildServiceProvider();
        var optionsAccessor = serviceProvider.GetRequiredService<IOptions<McpGatewayOptions>>();
        
        var ex = Assert.Throws<ConfigurationException>(() => optionsAccessor.Value);
        Assert.Equal("RoutePrefix is required", ex.Message);
    }

    [Fact]
    public void AddMcpGateway_WithValidConfiguration_ShouldBindCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>()
            {
                {"McpGateway:Department", "qc"},
                {"McpGateway:RoutePrefix", "/qc"},
                {"McpGateway:EnableHealthChecks", "true"},
                {"McpGateway:Ocelot:ConfigFile", "ocelot.json"},
                {"McpGateway:Auth:Provider", "JWT"},
                {"McpGateway:Auth:Enabled", "true"},
                {"McpGateway:TokenCache:Type", "Redis"},
                {"McpGateway:TokenCache:ExpirationMinutes", "120"},
                {"McpGateway:Audit:Enabled", "true"},
                {"McpGateway:Audit:LogPath", "/logs/audit"}
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddMcpGateway();

        // Act
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<McpGatewayOptions>>().Value;

        // Assert
        Assert.Equal("qc", options.Department);
        Assert.Equal("/qc", options.RoutePrefix);
        Assert.True(options.EnableHealthChecks);
        Assert.NotNull(options.Ocelot);
        Assert.Equal("ocelot.json", options.Ocelot?.ConfigFile);
        Assert.NotNull(options.Auth);
        Assert.Equal("JWT", options.Auth?.Provider);
        Assert.True(options.Auth?.Enabled);
        Assert.NotNull(options.TokenCache);
        Assert.Equal("Redis", options.TokenCache?.Type);
        Assert.Equal(120, options.TokenCache?.ExpirationMinutes);
        Assert.NotNull(options.Audit);
        Assert.True(options.Audit?.Enabled);
        Assert.Equal("/logs/audit", options.Audit?.LogPath);
    }

    [Fact]
    public void McpGatewayOptionsValidator_WithMismatchedRoutePrefix_ShouldLogWarning()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<McpGatewayOptionsValidator>>();
        var validator = new McpGatewayOptionsValidator(loggerMock.Object);
        
        var options = new McpGatewayOptions
        {
            Department = "report",
            RoutePrefix = "/mcp",
            EnableHealthChecks = true
        };

        // Act
        var result = validator.Validate("", options);

        // Assert
        Assert.True(result.Succeeded);
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("RoutePrefix")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()!),
            Times.Once);
    }
}