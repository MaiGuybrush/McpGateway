using ModelContextProtocol.Server;
using McpGateway.Infrastructure;
using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;

namespace McpGateway.Tools.Manual;

/// <summary>
/// Retrieve comprehensive user profile and account information
/// </summary>
/// <example>
/// Example 1: Get basic user info
/// {
///   "userId": 123
/// }
/// 
/// Example 2: Get detailed user profile with activity history
/// {
///   "userId": 456,
///   "includeProfileDetails": true
/// }
/// </example>
[McpTool("get_user_details")]
public class GetUserDetailsTool : ITool
{
    public string Name => "get_user_details";
    
    public string Description => """
        Retrieve comprehensive user profile and account information from the user management system.
        
        This tool fetches user details including:
        - Basic profile (name, email, user ID)
        - Account status and creation date
        - Last login information (when includeProfileDetails=true)
        - Activity history and account preferences (when includeProfileDetails=true)
        
        Use includeProfileDetails=true when you need complete user history for support or analytics.
        Use includeProfileDetails=false (default) for quick checks and basic information retrieval.
        """;
    
    private readonly IHttpClientFactory _httpClientFactory;
    
    public GetUserDetailsTool(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }
    
    /// <summary>
    /// Execute the tool to retrieve user information
    /// </summary>
    /// <param name="parameters">Tool parameters containing userId and optional includeProfileDetails flag</param>
    /// <returns>User profile data with requested detail level</returns>
    public async Task<dynamic> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var client = _httpClientFactory.CreateClient();
        
        // Primary parameter: User ID
        if (!parameters.TryGetValue("userId", out var userIdObj))
        {
            throw new ArgumentException("Missing required parameter: userId. Please provide a valid user ID (numeric).");
        }
        
        var userId = Convert.ToInt32(userIdObj);
        
        // Optional parameter: Include detailed profile
        var includeDetails = false;
        if (parameters.TryGetValue("includeProfileDetails", out var includeDetailsObj))
        {
            includeDetails = Convert.ToBoolean(includeDetailsObj);
        }
        
        // Construct API request
        var url = $"http://localhost:5001/api/users/{userId}";
        if (includeDetails)
        {
            url += $"?includeDetails=true";
        }
        
        try
        {
            var response = await client.GetAsync(url);
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new InvalidOperationException($"User {userId} not found in the system. Please verify the user ID.");
            }
            
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Failed to retrieve user data: {response.StatusCode}. Please try again or contact support.");
            }
            
            var content = await response.Content.ReadAsStringAsync();
            var userData = JsonSerializer.Deserialize<dynamic>(content);
            
            if (userData == null)
            {
                throw new InvalidOperationException("Received empty response from user service.");
            }
            
            return new 
            {
                success = true,
                user = userData,
                detailLevel = includeDetails ? "detailed" : "basic"
            };
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Network error while fetching user {userId}: {ex.Message}. Please check the service connectivity.", ex);
        }
    }
}

/// <summary>
/// Parameters for retrieving user details
/// </summary>
public class GetUserDetailsParameters
{
    [Description("Numeric user identifier (e.g., 12345)")]
    [JsonPropertyName("userId")]
    public int UserId { get; set; }
    
    [Description("Set to true to include comprehensive profile details including login history and account preferences. Default is false for basic info only.")]
    [JsonPropertyName("includeProfileDetails")]
    public bool? IncludeProfileDetails { get; set; }
}