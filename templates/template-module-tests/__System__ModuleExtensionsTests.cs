using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using McpGateway.Core.Subsystems;
using McpGateway.__Department__.__System__.Configuration;
using McpGateway.__Department__.__System__.Services;

namespace McpGateway.__Department__.__System__.Tests;

public class __System__ModuleExtensionsTests
{
    [Fact]
    public void Add__System__Subsystem_RegistersServicesAndOptionsProperly()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["McpGateway:Systems:__system__:Downstream:BaseUrl"] = "http://test-api.corp.local",
            ["McpGateway:Systems:__system__:Downstream:TimeoutSeconds"] = "15"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();

        // Act
        services.Add__System__Subsystem(configuration);
        var provider = services.BuildServiceProvider();

        // Assert
        var options = provider.GetService<IOptions<__System__Options>>()?.Value;
        Assert.NotNull(options);
        Assert.Equal("http://test-api.corp.local", options.Downstream.BaseUrl);
        Assert.Equal(15, options.Downstream.TimeoutSeconds);

        var service = provider.GetService<I__ToolClass__Service>();
        Assert.NotNull(service);

        var resolver = provider.GetService<IDownstreamUrlResolver>();
        Assert.NotNull(resolver);
        Assert.IsType<ConsulKvDownstreamResolver>(resolver);

        var registry = provider.GetService<IMcpSubsystemRegistry>();
        Assert.NotNull(registry);
        Assert.True(registry.ContainsSubsystem("__system__"));
    }

    [Fact]
    public async Task ConsulKvDownstreamResolver_NoConsulReachable_ReturnsFallbackBaseUrl()
    {
        var options = Options.Create(new __System__Options
        {
            Downstream = new DownstreamOptions { BaseUrl = "http://fallback.corp.local" },
            Consul = new ConsulOptions { Urls = new List<string>() }
        });
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<ConsulKvDownstreamResolver>();
        var resolver = new ConsulKvDownstreamResolver(options, logger);

        var url = await resolver.ResolveBaseUrlAsync();
        Assert.Equal("http://fallback.corp.local", url);
    }

    [Fact]
    public async Task ConsulServiceDiscoveryDownstreamResolver_NoConsulReachable_ReturnsFallbackBaseUrl()
    {
        var options = Options.Create(new __System__Options
        {
            Downstream = new DownstreamOptions { BaseUrl = "http://fallback.corp.local", ServiceName = "my-service" },
            Consul = new ConsulOptions { Urls = new List<string>() }
        });
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<ConsulServiceDiscoveryDownstreamResolver>();
        var resolver = new ConsulServiceDiscoveryDownstreamResolver(options, logger);

        var url = await resolver.ResolveBaseUrlAsync();
        Assert.Equal("http://fallback.corp.local", url);
    }
}
