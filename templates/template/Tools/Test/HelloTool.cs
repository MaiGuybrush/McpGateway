using System.ComponentModel;
using ModelContextProtocol.Server;

namespace McpGateway.__Department__.Tools.Test;

[McpServerToolType]
public class HelloTool
{
    [McpServerTool]
    [Description("簡單的問候工具")]
    public static string Hello(string name) => $"Hello, {name}!";
}
