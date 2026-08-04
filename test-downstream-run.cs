// Simple test to verify DownstreamClient works
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using McpGateway.Core.Configuration;
using McpGateway.Core.Downstream;
using McpGateway.Core.Tools;

class Test
{
    static async Task Main()
    {
        var httpClient = new HttpClient();
        var options = Options.Create(new OcelotOptions
        {
            BaseUrl = "https://jsonplaceholder.typicode.com",
            TimeoutSeconds = 10,
            Retry = new RetryOptions { Count = 2, BackoffMs = 100 }
        });

        var client = new DownstreamClient(httpClient, options, new ToolContext(
            UserId: "test-user",
            Department: "test-dept",
            Role: "test-role",
            TokenType: "test-token",
            AgentId: "test-agent",
            CorrelationId: "test-correlation"
        ));

        try
        {
            var result = await client.GetAsync<object>("/posts/1");
            Console.WriteLine("Success! DownstreamClient works.");
        }
        catch (Exception ex)
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}