using System.Net;
using System.Text.Json;
using McpGateway.Tools.Manual;
using ModelContextProtocol.Server;
using ModelContextProtocol.Types;
using RichardSzalay.MockHttp;

namespace McpGateway.Tests.Tools.Manual;

public class UserQueryToolTests
{
    private readonly GetUserDetailsTool _tool;
    private readonly MockHttpMessageHandler _mockHttp;
    
    public UserQueryToolTests()
    {
        _mockHttp = new MockHttpMessageHandler();
        var httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("http://api.test");
        
        _tool = new GetUserDetailsTool(httpClient);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidUserId_ReturnsUserData()
    {
        // Arrange
        var userId = "550e8400-e29b-41d4-a716-446655440000";
        var expectedResponse = new
        {
            id = userId,
            name = "Test User",
            email = "test@example.com",
            status = "active"
        };
        
        _mockHttp
            .When($"/api/users/{userId}")
            .Respond("application/json", JsonSerializer.Serialize(expectedResponse));

        // Act
        var result = await _tool.ExecuteAsync(userId, "all", false);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsError);
        Assert.Contains($"Successfully retrieved user details for ID: {userId}", 
            result.Content.First().Text);
        Assert.Contains("user_data", result.Metadata.Keys);
        Assert.Equal(200, result.Metadata["status_code"]);
    }

    [Fact]
    public async Task ExecuteAsync_WithFieldsParameter_FiltersFields()
    {
        // Arrange
        var userId = "550e8400-e29b-41d4-a716-446655440000";
        var fields = "id,name,email";
        
        _mockHttp
            .When($"/api/users/{userId}?fields={Uri.EscapeDataString(fields)}")
            .Respond("application/json", "{\"id\":\"550e8400-e29b-41d4-a716-446655440000\",\"name\":\"Test\",\"email\":\"test@example.com\"}");

        // Act
        var result = await _tool.ExecuteAsync(userId, fields, false);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsError);
        Assert.Equal(200, result.Metadata["status_code"]);
    }

    [Fact]
    public async Task ExecuteAsync_WithIncludeDeleted_IncludesDeletedUsers()
    {
        // Arrange
        var userId = "550e8400-e29b-41d4-a716-446655440000";
        
        _mockHttp
            .When($"/api/users/{userId}?includeDeleted=true")
            .Respond("application/json", "{\"id\":\"550e8400-e29b-41d4-a716-446655440000\",\"name\":\"Deleted User\",\"status\":\"deleted\"}");

        // Act
        var result = await _tool.ExecuteAsync(userId, "all", true);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsError);
        Assert.Equal(200, result.Metadata["status_code"]);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidUserIdFormat_ReturnsError()
    {
        // Arrange
        var invalidUserId = "invalid-uuid";

        // Act
        var result = await _tool.ExecuteAsync(invalidUserId, "all", false);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsError);
        Assert.Contains("Invalid userId format", result.Content.First().Text);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserNotFound_Returns404Error()
    {
        // Arrange
        var userId = "550e8400-e29b-41d4-a716-446655440000";
        
        _mockHttp
            .When($"/api/users/{userId}")
            .Respond(HttpStatusCode.NotFound, "application/json", "{\"error\":\"User not found\"}");

        // Act
        var result = await _tool.ExecuteAsync(userId, "all", false);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsError);
        Assert.Contains($"User with ID '{userId}' not found", result.Content.First().Text);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUnauthorized_ReturnsAuthError()
    {
        // Arrange
        var userId = "550e8400-e29b-41d4-a716-446655440000";
        
        _mockHttp
            .When($"/api/users/{userId}")
            .Respond(HttpStatusCode.Unauthorized, "application/json", "{\"error\":\"Unauthorized\"}");

        // Act
        var result = await _tool.ExecuteAsync(userId, "all", false);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsError);
        Assert.Contains("Authentication failed", result.Content.First().Text);
    }

    [Fact]
    public async Task ExecuteAsync_WhenForbidden_ReturnsPermissionError()
    {
        // Arrange
        var userId = "550e8400-e29b-41d4-a716-446655440000";
        
        _mockHttp
            .When($"/api/users/{userId}")
            .Respond(HttpStatusCode.Forbidden, "application/json", "{\"error\":\"Access denied\"}");

        // Act
        var result = await _tool.ExecuteAsync(userId, "all", false);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsError);
        Assert.Contains("Access denied", result.Content.First().Text);
    }

    [Fact]
    public async Task ExecuteAsync_WhenServerError_Returns500Error()
    {
        // Arrange
        var userId = "550e8400-e29b-41d4-a716-446655440000";
        
        _mockHttp
            .When($"/api/users/{userId}")
            .Respond(HttpStatusCode.InternalServerError, "application/json", "{\"error\":\"Internal server error\"}");

        // Act
        var result = await _tool.ExecuteAsync(userId, "all", false);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsError);
        Assert.Contains("API server error occurred", result.Content.First().Text);
    }

    [Fact]
    public async Task ExecuteAsync_WhenServiceUnavailable_Returns503Error()
    {
        // Arrange
        var userId = "550e8400-e29b-41d4-a716-446655440000";
        
        _mockHttp
            .When($"/api/users/{userId}")
            .Respond(HttpStatusCode.ServiceUnavailable, "application/json", "{\"error\":\"Service unavailable\"}");

        // Act
        var result = await _tool.ExecuteAsync(userId, "all", false);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsError);
        Assert.Contains("API service is temporarily unavailable", result.Content.First().Text);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTimeout_ReturnsTimeoutError()
    {
        // Arrange
        var userId = "550e8400-e29b-41d4-a716-446655440000";
        
        _mockHttp
            .When($"/api/users/{userId}")
            .Respond(HttpStatusCode.GatewayTimeout, "application/json", "{\"error\":\"Gateway timeout\"}");

        // Act
        var result = await _tool.ExecuteAsync(userId, "all", false);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsError);
        Assert.Contains("API request timed out", result.Content.First().Text);
    }

    [Fact]
    public void Examples_ContainsValidExamples()
    {
        // Act & Assert
        var examples = GetUserDetailsExamples.Examples;
        
        Assert.NotNull(examples);
        Assert.Equal(3, examples.Length);
        
        // Check first example
        var basicExample = examples[0];
        Assert.Contains("基本用戶查詢", basicExample.Description);
        Assert.Contains("userId", basicExample.Input.Keys);
        Assert.Contains("fields", basicExample.Input.Keys);
        
        // Check fields example
        var fieldsExample = examples[1];
        Assert.Contains("查詢特定欄位", fieldsExample.Description);
        
        // Check includeDeleted example
        var deletedExample = examples[2];
        Assert.Contains("查詢已刪除用戶", deletedExample.Description);
        Assert.Contains("includeDeleted", deletedExample.Input.Keys);
    }
}