using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using McpGateway.Core.Configuration;
using McpGateway.Core.Downstream;
using McpGateway.Core.Tools;
using Moq.Protected;
using Moq;
using Xunit;

namespace McpGateway.Tests.Downstream;

public class DownstreamClientTests : UnitTestBase
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly IOptions<OcelotOptions> _ocelotOptions;
    private readonly ToolContext _testContext;

    public DownstreamClientTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://api.example.com")
        };
        
        _ocelotOptions = Options.Create(new OcelotOptions
        {
            BaseUrl = "https://api.example.com",
            TimeoutSeconds = 10,
            Retry = new RetryOptions { Count = 2, BackoffMs = 100 }
        });

        _testContext = new ToolContext(
            UserId: "user123",
            Department: "report",
            Role: "analyst",
            TokenType: "JWT",
            AgentId: "agent456",
            CorrelationId: "correlation-789"
        );
    }

    [Fact]
    public async Task GetAsync_WithContext_InjectHeaders()
    {
        // Arrange
        var responseContent = JsonSerializer.Serialize(new { data = "test" });
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", 
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Headers.Contains("X-User-Id") &&
                    req.Headers.Contains("X-User-Department") &&
                    req.Headers.Contains("X-User-Role") &&
                    req.Headers.Contains("X-Auth-Type") &&
                    req.Headers.Contains("X-Correlation-Id") &&
                    req.Headers.Contains("X-Gateway-Department")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent)
            });

        var client = new DownstreamClient(_httpClient, _ocelotOptions, _testContext);

        // Act
        var result = await client.GetAsync<object>("/api/test");

        // Assert
        _httpMessageHandlerMock.VerifyAll();
    }

    [Fact]
    public async Task GetAsync_WithoutContext_NoHeadersInjected()
    {
        // Arrange
        var responseContent = JsonSerializer.Serialize(new { data = "test" });
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", 
                ItExpr.Is<HttpRequestMessage>(req =>
                    !req.Headers.Contains("X-User-Id")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent)
            });

        var client = new DownstreamClient(_httpClient, _ocelotOptions, null);

        // Act
        var result = await client.GetAsync<object>("/api/test");

        // Assert
        _httpMessageHandlerMock.VerifyAll();
    }

    [Fact]
    public async Task GetAsync_RetryableStatusCode_Retries()
    {
        // Arrange
        var callCount = 0;
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", 
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount < 3)
                {
                    return new HttpResponseMessage { StatusCode = HttpStatusCode.InternalServerError };
                }
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(new { success = true }))
                };
            });

        var client = new DownstreamClient(_httpClient, _ocelotOptions, _testContext);

        // Act
        var result = await client.GetAsync<object>("/api/test");

        // Assert
        Assert.Equal(3, callCount);
    }

    [Fact]
    public async Task PostAsync_NoRetryOnFailure()
    {
        // Arrange
        var callCount = 0;
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", 
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                return new HttpResponseMessage { StatusCode = HttpStatusCode.InternalServerError };
            });

        var client = new DownstreamClient(_httpClient, _ocelotOptions, _testContext);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await client.PostAsync<object>("/api/test", new { data = "test" }));
        
        Assert.Equal(1, callCount); // Should not retry
    }

    [Fact]
    public async Task PutAsync_RetryableLikeGet()
    {
        // Arrange
        var callCount = 0;
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", 
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount < 2)
                {
                    return new HttpResponseMessage { StatusCode = HttpStatusCode.RequestTimeout };
                }
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(new { success = true }))
                };
            });

        var client = new DownstreamClient(_httpClient, _ocelotOptions, _testContext);

        // Act
        var result = await client.PutAsync<object>("/api/test", new { data = "test" });

        // Assert
        Assert.Equal(2, callCount); // Should retry once
    }

    [Fact]
    public async Task DeleteAsync_RetryableLikeGet()
    {
        // Arrange
        var callCount = 0;
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", 
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount < 2)
                {
                    return new HttpResponseMessage { StatusCode = HttpStatusCode.TooManyRequests };
                }
                return new HttpResponseMessage { StatusCode = HttpStatusCode.NoContent };
            });

        var client = new DownstreamClient(_httpClient, _ocelotOptions, _testContext);

        // Act
        await client.DeleteAsync("/api/test");

        // Assert
        Assert.Equal(2, callCount); // Should retry once
    }

    [Fact]
    public async Task GetAsync_WithQueryParameters_BuildsCorrectUrl()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", 
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken ct) =>
            {
                capturedRequest = req;
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(new { success = true }))
                };
            });

        var client = new DownstreamClient(_httpClient, _ocelotOptions, _testContext);

        // Act
        var query = new { filter = "active", page = 1, size = 10 };
        await client.GetAsync<object>("/api/items", query);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Contains("filter=active", capturedRequest!.RequestUri!.Query);
        Assert.Contains("page=1", capturedRequest.RequestUri.Query);
        Assert.Contains("size=10", capturedRequest.RequestUri.Query);
    }

    [Fact]
    public async Task GetAsync_DeserializesResponseCorrectly()
    {
        // Arrange
        var expected = new TestResponse { Id = 123, Name = "Test Item" };
        var responseContent = JsonSerializer.Serialize(expected);
        
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", 
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent)
            });

        var client = new DownstreamClient(_httpClient, _ocelotOptions, _testContext);

        // Act
        var result = await client.GetAsync<TestResponse>("/api/test");

        // Assert
        Assert.Equal(expected.Id, result.Id);
        Assert.Equal(expected.Name, result.Name);
    }

    [Fact]
    public async Task GetAsync_NonRetryableStatusCode_NoRetry()
    {
        // Arrange
        var callCount = 0;
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", 
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                return new HttpResponseMessage { StatusCode = HttpStatusCode.BadRequest };
            });

        var client = new DownstreamClient(_httpClient, _ocelotOptions, _testContext);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await client.GetAsync<object>("/api/test"));
        
        Assert.Equal(1, callCount); // Should not retry
    }

    private class TestResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}