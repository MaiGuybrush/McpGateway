using System.Net;
using System.Text;
using Xunit;

namespace McpGateway.Report.E2ETests;

/// <summary>
/// Basic E2E tests for MCP Server capabilities
/// MANUAL RUN: Requires McpGateway.Report running on localhost:5100
/// </summary>
public class McpServerE2ETests
{
    private const string GatewayUrl = "http://localhost:5100";
    private readonly HttpClient _client = new();

    public McpServerE2ETests()
    {
        _client.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    [Fact(Skip = "MANUAL RUN: Requires McpGateway.Report running on localhost:5100")]
    public async Task McpServer_ShouldSupportToolsList()
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
        Assert.Contains("tools", data);
        Assert.Contains("query_wip", data);
    }

    [Fact(Skip = "MANUAL RUN: Requires McpGateway.Report running on localhost:5100")]
    public async Task McpServer_ShouldSupportToolsCall()
    {
        // Arrange
        var jsonContent = """{"jsonrpc":"2.0","method":"tools/call","params":{"name":"query_wip","arguments":{"workCenter":"WC-E2E"}},"id":2}""";
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync(GatewayUrl, content);
        
        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadAsStringAsync();
        var data = ParseSseData(result);
        
        Assert.NotNull(data);
        Assert.Contains("result", data);
        Assert.Contains("content", data);
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