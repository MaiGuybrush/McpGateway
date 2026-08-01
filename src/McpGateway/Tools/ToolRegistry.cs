using ModelContextProtocol.Server;

namespace McpGateway.Tools;

public static class ToolRegistry
{
    public static IEnumerable<Type> GetManualTools()
    {
        return new[]
        {
            typeof(Manual.GetUserDetailsTool),
            typeof(Manual.PlaceNewOrderTool)
        };
    }
}