using ModelContextProtocol.Server;
using McpGateway.Infrastructure;
using System.ComponentModel;
using System.Text.Json;

namespace McpGateway.Tools.Mechanical;

/// <summary>
/// Auto-generated from OpenAPI spec: GET /api/users/{id}
/// </summary>
public class UserQueryTool : ITool
{
    public string Name => "get_api_users_id";
    
    public string Description => "Get user information by ID";
    
    private readonly IHttpClientFactory _httpClientFactory;
    
    public UserQueryTool(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }
    
    public async Task<dynamic> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var client = _httpClientFactory.CreateClient();
        
        // Extract parameters
        if (!parameters.TryGetValue("id", out var idObj))
        {
            throw new ArgumentException("Missing required parameter: id");
        }
        
        var id = Convert.ToInt32(idObj);
        
        var includeDetails = false;
        if (parameters.TryGetValue("includeDetails", out var includeDetailsObj))
        {
            includeDetails = Convert.ToBoolean(includeDetailsObj);
        }
        
        // Build URL
        var url = $"http://localhost:5001/api/users/{id}";
        if (includeDetails)
        {
            url += $"?includeDetails=true";
        }
        
        var response = await client.GetAsync(url);
        
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"API call failed: {response.StatusCode}");
        }
        
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<dynamic>(content) ?? new { };
    }
}

/// <summary>
/// Input parameters for get_api_users_id
/// </summary>
public class UserQueryParameters
{
    [Description("Unique identifier of the user")]
    public int Id { get; set; }
    
    [Description("Whether to include additional user details")]
    public bool? IncludeDetails { get; set; }
}