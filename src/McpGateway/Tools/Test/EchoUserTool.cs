using System.ComponentModel;
using System.Text.Json.Serialization;
using McpGateway.Core.Downstream;
using McpGateway.Core.Tools;

namespace McpGateway.Tools.Test;

/// <summary>
/// Echo tool for end-to-end testing with JWT authentication
/// </summary>
[McpTool("report_echo_user")]
public class EchoUserTool : ToolBase<EchoUserInput, EchoUserOutput>
{
    public override string Name => "report_echo_user";
    
    public override string Description => """
        Echo user information for end-to-end testing.
        Returns the input message along with context information.
        """;

    public EchoUserTool() : base((IHttpClientFactory)null!)
    {
    }

    public override Task<EchoUserOutput> ExecuteAsync(EchoUserInput input, ToolContext context, CancellationToken cancellationToken = default)
    {
        var result = new EchoUserOutput
        {
            Message = input.Message,
            UserId = context.UserId,
            Department = context.Department,
            Role = context.Role,
            TokenType = context.TokenType,
            Timestamp = DateTime.UtcNow
        };

        return Task.FromResult(result);
    }
}

public class EchoUserInput
{
    [Description("The message to echo")]
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

public class EchoUserOutput
{
    [Description("The original message")]
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [Description("User ID from JWT token")]
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [Description("Department from JWT token")]
    [JsonPropertyName("department")]
    public string Department { get; set; } = string.Empty;

    [Description("Role from JWT token")]
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [Description("Token type")]
    [JsonPropertyName("tokenType")]
    public string TokenType { get; set; } = string.Empty;

    [Description("When the echo was generated")]
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }
}
