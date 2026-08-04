using System.Net;
using System.Text;
using Xunit;

namespace McpGateway.Report.IntegrationTests;

/// <summary>
/// Basic MCP tool registration and execution tests for McpGateway.Report
/// MANUAL RUN: Requires McpGateway.Report to be running on localhost:5100
/// </summary>
public class McpToolRegistrationTests
{
    private const string GatewayUrl = "http://localhost:5100";
    private readonly HttpClient _client = new();

    public McpToolRegistrationTests()
    {
        _client.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    [Fact(Skip = "MANUAL RUN: Requires McpGateway.Report running on localhost:5100")]
    public async Task ToolsList_ShouldReturnQueryWipTool()
    {
        // Arrange
        var jsonContent = """{"jsonrpc":"2.0","method":"tools/list","id":1}""";
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync(GatewayUrl, content);
        
        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadAsStringAsync();
        var data = ParseSseData(result);
        
        Assert.NotNull(data);
        Assert.Contains("query_wip", data);
        Assert.Contains("在製品", data);
    }

    [Fact(Skip = "MANUAL RUN: Requires McpGateway.Report running on localhost:5100")]
    public async Task QueryWipTool_WithParameters_ShouldReturnFormattedReport()
    {
        // Arrange
        var jsonContent = """{"jsonrpc":"2.0","method":"tools/call","params":{"name":"query_wip","arguments":{"workCenter":"WC-TEST-01","productLine":"LINE-A"}},"id":2}""";
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync(GatewayUrl, content);
        
        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadAsStringAsync();
        var data = ParseSseData(result);
        
        Assert.NotNull(data);
        Assert.Contains("WIP 在製品報告", data);
        Assert.Contains("WC-TEST-01", data);
        Assert.Contains("LINE-A", data);
    }

    [Fact(Skip = "MANUAL RUN: Requires McpGateway.Report running on localhost:5100")]
    public async Task QueryWipTool_WithNullParameters_ShouldReturnDefaultReport()
    {
        // Arrange
        var jsonContent = """{"jsonrpc":"2.0","method":"tools/call","params":{"name":"query_wip","arguments":{}},"id":3}""";
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync(GatewayUrl, content);
        
        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadAsStringAsync();
        var data = ParseSseData(result);
        
        Assert.NotNull(data);
        Assert.Contains("WIP 在製品報告", data);
    }

    [Fact(Skip = "MANUAL RUN: Requires McpGateway.Report running on localhost:5100")]
    public async Task HelloTool_ShouldReturnGreeting()
    {
        // Arrange
        var jsonContent = """{"jsonrpc":"2.0","method":"tools/call","params":{"name":"hello","arguments":{"name":"MCP Test"}},"id":4}""";
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync(GatewayUrl, content);
        
        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadAsStringAsync();
        var data = ParseSseData(result);
        
        Assert.NotNull(data);
        Assert.Contains("Hello, MCP Test", data);
    }

    // Helper to parse SSE formatted JSON response
    private static string? ParseSseData(string sseContent)
    {
        foreach (var line in sseContent.Split('\n'))
        {
            if (line.StartsWith("data: "))
            {
                return line.Substring("data: ".Length);
            }
        }
        return null;
    }
}