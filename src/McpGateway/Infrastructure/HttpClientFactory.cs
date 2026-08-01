namespace McpGateway.Infrastructure;

public static class HttpClientFactory
{
    public static HttpClient Create(string baseUrl)
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
        
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        client.DefaultRequestHeaders.Add("User-Agent", "McpGateway/1.0");
        
        return client;
    }
}