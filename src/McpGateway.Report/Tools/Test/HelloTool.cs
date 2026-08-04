using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace McpGateway.Report.Tools.Test;

[McpServerToolType]
public class HelloTool
{
    [McpServerTool]
    [Description("簡單的問候工具")]
    public static string Hello(string name) => $"Hello, {name}!";
}