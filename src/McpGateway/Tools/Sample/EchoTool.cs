using McpGateway.Core.Tools;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace McpGateway.Tools.Sample;

/// <summary>
/// Simple echo tool for testing the new ToolBase framework
/// </summary>
[McpTool("echo")]
public class EchoTool : ToolBase<EchoInput, EchoOutput>
{
    public override string Name => "echo";
    
    public override string Description => """
        Echoes back the input message with a timestamp.
        Useful for testing the tool framework.
        """;
    
    public override ToolInputSchema InputSchema => new()
    {
        Type = "object",
        Properties = new Dictionary<string, object>
        {
            ["message"] = new 
            { 
                type = "string", 
                description = "The message to echo" 
            },
            ["count"] = new 
            { 
                type = "number", 
                description = "How many times to echo (optional)", 
                @default = 1 
            }
        },
        Required = new[] { "message" }
    };

    public EchoTool(IHttpClientFactory httpClientFactory) : base(httpClientFactory)
    {
    }

    public override Task<EchoOutput> ExecuteAsync(EchoInput input, ToolContext context, CancellationToken cancellationToken = default)
    {
        var count = input.Count ?? 1;
        var echoes = new List<string>();
        
        for (int i = 0; i < count; i++)
        {
            echoes.Add($"[{i + 1}] {input.Message}");
        }

        var result = new EchoOutput
        {
            Echoes = echoes,
            Timestamp = DateTime.UtcNow,
            UserId = context.UserId,
            Department = context.Department
        };

        return Task.FromResult(result);
    }
}

public class EchoInput
{
    [Description("The message to echo")]
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [Description("How many times to echo")]
    [JsonPropertyName("count")]
    public int? Count { get; set; }
}

public class EchoOutput
{
    [Description("List of echoed messages")]
    [JsonPropertyName("echoes")]
    public List<string> Echoes { get; set; } = new();

    [Description("When the echo was generated")]
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [Description("User ID from context")]
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [Description("Department from context")]
    [JsonPropertyName("department")]
    public string Department { get; set; } = string.Empty;
}