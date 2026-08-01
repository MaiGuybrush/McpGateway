using ModelContextProtocol.Server;
using McpGateway.Infrastructure;
using System.ComponentModel;
using System.Text.Json;

namespace McpGateway.Tools.Mechanical;

/// <summary>
/// Auto-generated from OpenAPI spec: POST /api/orders
/// </summary>
public class OrderCreateTool : ITool
{
    public string Name => "post_api_orders";
    
    public string Description => "Creates a new order with items and shipping information.";
    
    private readonly IHttpClientFactory _httpClientFactory;
    
    public OrderCreateTool(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }
    
    public async Task<dynamic> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var client = _httpClientFactory.CreateClient();
        
        // Extract request body
        if (!parameters.TryGetValue("requestBody", out var requestBodyObj))
        {
            throw new ArgumentException("Missing required parameter: requestBody");
        }
        
        var requestBody = JsonSerializer.Serialize(requestBodyObj);
        var content = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json");
        
        var url = "http://localhost:5001/api/orders";
        var response = await client.PostAsync(url, content);
        
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"API call failed: {response.StatusCode}");
        }
        
        var responseContent = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<dynamic>(responseContent) ?? new { };
    }
}

/// <summary>
/// Input parameters for post_api_orders
/// </summary>
public class OrderCreateParameters
{
    [Description("Request body for creating an order")]
    public OrderRequestBody RequestBody { get; set; } = new();
}

public class OrderRequestBody
{
    [Description("ID of the user placing the order")]
    public int UserId { get; set; }
    
    [Description("List of order items")]
    public List<OrderItem> Items { get; set; } = new();
    
    [Description("Shipping address for the order")]
    public ShippingAddress ShippingAddress { get; set; } = new();
    
    [Description("Order priority: Standard or Express")]
    public string? Priority { get; set; }
}

public class OrderItem
{
    [Description("Product identifier")]
    public string ProductId { get; set; } = string.Empty;
    
    [Description("Product name")]
    public string ProductName { get; set; } = string.Empty;
    
    [Description("Quantity ordered")]
    public int Quantity { get; set; }
    
    [Description("Price per unit")]
    public decimal UnitPrice { get; set; }
    
    [Description("Optional notes for the item")]
    public string? Notes { get; set; }
}

public class ShippingAddress
{
    [Description("Street address")]
    public string Street { get; set; } = string.Empty;
    
    [Description("City name")]
    public string City { get; set; } = string.Empty;
    
    [Description("State or province")]
    public string State { get; set; } = string.Empty;
    
    [Description("Postal/zip code")]
    public string ZipCode { get; set; } = string.Empty;
    
    [Description("Country name")]
    public string Country { get; set; } = string.Empty;
}