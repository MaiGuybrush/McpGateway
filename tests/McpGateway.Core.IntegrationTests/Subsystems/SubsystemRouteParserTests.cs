using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using McpGateway.Core.Subsystems;
using Xunit;

namespace McpGateway.Core.IntegrationTests.Subsystems;

public class SubsystemRouteParserTests
{
    [Theory]
    [InlineData("/mfg/mes/mcp", "mfg", "mes")]
    [InlineData("/MFG/MES/MCP", "mfg", "mes")]
    [InlineData("/mfg/wms/mcp", "mfg", "wms")]
    [InlineData("/mfg/eap/mcp/", "mfg", "eap")]
    [InlineData("/mfg/systems/mes/mcp", "mfg", "mes")]
    [InlineData("/mes/mcp", "mfg", "mes")]
    public void ExtractSubsystem_StandardSubsystemPaths_ReturnsSubsystem(string path, string dept, string expectedSubsystem)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        var result = SubsystemRouteParser.ExtractSubsystem(context, dept);

        Assert.Equal(expectedSubsystem, result);
    }

    [Theory]
    [InlineData("/mfg/mcp", "mfg")]
    [InlineData("/mcp", "mfg")]
    [InlineData("/health/live", "mfg")]
    [InlineData("/health/ready", "mfg")]
    [InlineData("", "mfg")]
    public void ExtractSubsystem_DepartmentRootOrNonMcpPaths_ReturnsNull(string path, string dept)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        var result = SubsystemRouteParser.ExtractSubsystem(context, dept);

        Assert.Null(result);
    }

    [Fact]
    public void ExtractSubsystem_WithRouteValues_PrefersRouteValue()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/some/random/path";
        context.Request.RouteValues = new RouteValueDictionary
        {
            ["system"] = "mes"
        };

        var result = SubsystemRouteParser.ExtractSubsystem(context, "mfg");

        Assert.Equal("mes", result);
    }
}
