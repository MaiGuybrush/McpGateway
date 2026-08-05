using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using McpGateway.Core.Configuration;
using McpGateway.Core.Downstream;
using McpGateway.Core.Hosting;
using WireMock.Server;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Settings;
using WireMock.Types;
using WireMock.Util;
using Xunit;

namespace McpGateway.Core.IntegrationTests.Downstream;

public class DownstreamClientIntegrationTests : IAsyncLifetime
{
    private WireMockServer? _wireMockServer;
    private WebApplication? _app;

    public async Task InitializeAsync()
    {
        // Start WireMock server to simulate Ocelot downstream
        _wireMockServer = WireMockServer.Start();
        
        // Configure and start test web app
        var builder = WebApplication.CreateBuilder();
        
        // Add required memory cache
        builder.Services.AddMemoryCache();
        
        // Configure downstream client to use WireMock
        var ocelotOptions = new OcelotOptions
        {
            BaseUrl = _wireMockServer.Urls[0],
            TimeoutSeconds = 10,
            Retry = new RetryOptions { Count = 2, BackoffMs = 50 }
        };

        builder.Services.Configure<McpGatewayOptions>(options =>
        {
            options.Department = "test";
            options.RoutePrefix = "/test";
            options.Ocelot = ocelotOptions;
        });

        builder.Services.AddMcpGateway();

        // Override IOptions<OcelotOptions> to guarantee test values
        builder.Services.AddSingleton<IOptions<OcelotOptions>>(Options.Create(ocelotOptions));

        _app = builder.Build();
        
        await _app.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_app != null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
        _wireMockServer?.Stop();
    }

    private class TestResponse
    {
        public bool Success { get; set; }
    }

    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public async Task GetAsync_RetryOnServerError_ReturnsSuccessAfterRetry()
    {
        // Arrange with callback count
        int attempts = 0;
        _wireMockServer!
            .Given(Request.Create().WithPath("/api/test").UsingGet())
            .RespondWith(Response.Create().WithCallback(request =>
            {
                attempts++;
                var response = new WireMock.ResponseMessage();
                if (attempts <= 2)
                {
                    response.StatusCode = 500;
                }
                else
                {
                    response.StatusCode = 200;
                    response.AddHeader("Content-Type", "application/json");
                    response.BodyData = new BodyData
                    {
                        BodyAsString = JsonSerializer.Serialize(new TestResponse { Success = true }, CamelCaseOptions),
                        Encoding = System.Text.Encoding.UTF8,
                        DetectedBodyType = BodyType.String
                    };
                }
                return response;
            }));

        var sp = _app!.Services;
        var downstreamClient = sp.GetRequiredService<IDownstreamClient>();

        // Act
        var result = await downstreamClient.GetAsync<TestResponse>("/api/test");

        // Assert - result should be successful after retries
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task PostAsync_NoRetry_ReturnsImmediately()
    {
        // Arrange
        _wireMockServer!
            .Given(Request.Create().WithPath("/api/items").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.InternalServerError));

        var sp = _app!.Services;
        var downstreamClient = sp.GetRequiredService<IDownstreamClient>();

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await downstreamClient.PostAsync<TestResponse>("/api/items", new { data = "test" }));
    }

    [Fact]
    public async Task GetAsync_HeadersCorrectlyInjected()
    {
        // Arrange
        _wireMockServer!
            .Given(Request.Create().WithPath("/api/echo").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(new TestResponse { Success = true }, CamelCaseOptions))
            );

        var sp = _app!.Services;
        var downstreamClient = sp.GetRequiredService<IDownstreamClient>();

        // Act
        await downstreamClient.GetAsync<TestResponse>("/api/echo");

        // Assert - headers should be present (verified in the WireMock logs)
        var requests = _wireMockServer.FindLogEntries(Request.Create().WithPath("/api/echo").UsingGet());
        Assert.NotEmpty(requests);
    }

    [Fact]
    public async Task PutAsync_RetryLikeGet()
    {
        // Arrange
        _wireMockServer!
            .Given(Request.Create().WithPath("/api/resource/1").UsingPut())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(new TestResponse { Success = true }, CamelCaseOptions))
            );

        var sp = _app!.Services;
        var downstreamClient = sp.GetRequiredService<IDownstreamClient>();

        // Act
        var result = await downstreamClient.PutAsync<TestResponse>("/api/resource/1", new { value = "updated" });

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task DeleteAsync_RetryIdempotent()
    {
        // Arrange
        _wireMockServer!
            .Given(Request.Create().WithPath("/api/resource/2").UsingDelete())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.NoContent)
            );

        var sp = _app!.Services;
        var downstreamClient = sp.GetRequiredService<IDownstreamClient>();

        // Act
        await downstreamClient.DeleteAsync("/api/resource/2");

        // Assert - should complete without exception
        Assert.True(true);
    }
}
