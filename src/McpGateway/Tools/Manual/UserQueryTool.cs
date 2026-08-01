using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using ModelContextProtocol.Types;

namespace McpGateway.Tools.Manual;

/// <summary>
/// 獲取用戶詳細資訊，包括個人資料、聯絡資訊和帳戶狀態
/// </summary>
[Description("獲取用戶詳細資訊，包括個人資料、聯絡資訊和帳戶狀態")]
[McpToolType]
public class GetUserDetailsTool
{
    private readonly HttpClient _httpClient;
    
    public GetUserDetailsTool(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    [Description("根據用戶 ID 獲取完整的用戶詳細資訊")]
    [McpTool("get_user_details")]
    public async Task<CallToolResponse> ExecuteAsync(
        [Description("用戶的 unique identifier，必須是有效的 UUID 格式（例如: 550e8400-e29b-41d4-a716-446655440000）")]
        string userId,
        
        [Description("選擇性：指定回應中要包含的欄位，以逗號分隔。例如: 'id,name,email' 或 'all'（回傳所有欄位）")]
        string? fields = "all",
        
        [Description("選擇性：是否包含已刪除/停用的用戶資訊（預設: false）")]
        bool includeDeleted = false)
    {
        try
        {
            // 驗證 userId 格式
            if (!Guid.TryParse(userId, out _))
            {
                return CreateErrorResponse($"Invalid userId format: '{userId}'. Must be a valid UUID.");
            }

            // 構建 query parameters
            var queryParams = new List<string>();
            if (!string.IsNullOrWhiteSpace(fields) && fields != "all")
            {
                queryParams.Add($"fields={Uri.EscapeDataString(fields)}");
            }
            if (includeDeleted)
            {
                queryParams.Add("includeDeleted=true");
            }

            var queryString = queryParams.Any() ? $"?{string.Join("&", queryParams)}" : "";
            var url = $"/api/users/{userId}{queryString}";

            // 發送 GET 請求
            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var userData = JsonSerializer.Deserialize<JsonElement>(content);
                return new CallToolResponse(
                    content: [Content.CreateText($"Successfully retrieved user details for ID: {userId}")],
                    isError: false,
                    metadata: new Dictionary<string, object>
                    {
                        ["user_data"] = userData,
                        ["status_code"] = (int)response.StatusCode
                    }
                );
            }

            // 處理錯誤情況
            return HandleApiError(response.StatusCode, content, userId);
        }
        catch (HttpRequestException ex)
        {
            return CreateErrorResponse($"Network error occurred: {ex.Message}. Please check the API connectivity.");
        }
        catch (JsonException ex)
        {
            return CreateErrorResponse($"Failed to parse API response: {ex.Message}");
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"Unexpected error: {ex.Message}");
        }
    }

    private CallToolResponse HandleApiError(HttpStatusCode statusCode, string content, string userId)
    {
        return statusCode switch
        {
            HttpStatusCode.NotFound => CreateErrorResponse($"User with ID '{userId}' not found."),
            HttpStatusCode.BadRequest => CreateErrorResponse($"Invalid request parameters. API response: {content}"),
            HttpStatusCode.Unauthorized => CreateErrorResponse("Authentication failed. Please check API credentials."),
            HttpStatusCode.Forbidden => CreateErrorResponse($"Access denied for user '{userId}'. Insufficient permissions."),
            HttpStatusCode.InternalServerError => CreateErrorResponse($"API server error occurred. Details: {content}"),
            HttpStatusCode.ServiceUnavailable => CreateErrorResponse("API service is temporarily unavailable. Please try again later."),
            HttpStatusCode.GatewayTimeout => CreateErrorResponse("API request timed out. The service may be overloaded."),
            _ => CreateErrorResponse($"API request failed with status {statusCode}. Details: {content}")
        };
    }

    private CallToolResponse CreateErrorResponse(string message)
    {
        return new CallToolResponse(
            content: [Content.CreateText($"ERROR: {message}")],
            isError: true
        );
    }
}

/// <summary>
/// 提供 MCP Tool 的範例使用方式
/// </summary>
public static class GetUserDetailsExamples
{
    public static readonly Example[] Examples = new[]
    {
        new Example
        {
            Description = "基本用戶查詢 - 獲取所有欄位",
            Input = new Dictionary<string, object>
            {
                ["userId"] = "550e8400-e29b-41d4-a716-446655440000",
                ["fields"] = "all"
            }
        },
        new Example
        {
            Description = "查詢特定欄位 - 只獲取必要資訊",
            Input = new Dictionary<string, object>
            {
                ["userId"] = "550e8400-e29b-41d4-a716-446655440000",
                ["fields"] = "id,name,email,phone"
            }
        },
        new Example
        {
            Description = "查詢已刪除用戶 - 包含停用的帳戶",
            Input = new Dictionary<string, object>
            {
                ["userId"] = "550e8400-e29b-41d4-a716-446655440000",
                ["includeDeleted"] = true
            }
        }
    };
}