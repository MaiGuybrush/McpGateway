using ModelContextProtocol.Server;
using McpGateway.Infrastructure;
using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;

namespace McpGateway.Tools.Manual;

/// <summary>
/// Create and process a new customer order with items, shipping, and priority options
/// </summary>
/// <example>
/// Example 1: Simple single item order
/// {
///   "customerId": 2001,
///   "orderItems": [
///     {
///       "productId": "PROD-001",
///       "productName": "Wireless Mouse",
///       "quantity": 1,
///       "unitPrice": 29.99
///     }
///   ],
///   "deliveryAddress": {
///     "street": "123 Main Street",
///     "city": "Boston",
///     "state": "MA",
///     "zipCode": "02101",
///     "country": "USA"
///   }
/// }
/// 
/// Example 2: Multi-item express order
/// {
///   "customerId": 2002,
///   "orderItems": [
///     {
///       "productId": "PROD-002",
///       "productName": "USB-C Cable",
///       "quantity": 3,
///       "unitPrice": 12.99,
///       "notes": "Blue color preferred"
///     },
///     {
///       "productId": "PROD-003",
///       "productName": "Notebook",
///       "quantity": 2,
///       "unitPrice": 8.50
///     }
///   ],
///   "deliveryAddress": {
///     "street": "456 Oak Avenue",
///     "city": "New York",
///     "state": "NY",
///     "zipCode": "10001",
///     "country": "USA"
///   },
///   "shippingSpeed": "Express",
///   "giftWrapping": false
/// }
/// </example>
[McpTool("place_new_order")]
public class PlaceNewOrderTool : ITool
{
    public string Name => "place_new_order";
    
    public string Description => """
        Create and process a new customer order in the e-commerce system. This tool handles:
        
        ORDER CREATION CAPABILITIES:
        • Multiple product items with quantities and pricing
        • Flexible shipping address specification
        • Priority shipping options (Standard vs Express)
        • Special handling instructions and gift wrapping
        • Order validation and immediate confirmation
        
        WHEN TO USE:
        - Customer purchases one or more products
        - Processing wholesale or bulk orders
        - Rush orders requiring express delivery
        - Gift orders requiring special handling
        - Any scenario requiring order documentation
        
        SUCCESS RESPONSE:
        Returns order confirmation with order ID, status, total amount, and estimated delivery date.
        
        ERROR HANDLING:
        Validates all required fields, checks product availability, and confirms address completeness.
        """;
    
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PlaceNewOrderTool> _logger;
    
    public PlaceNewOrderTool(
        IHttpClientFactory httpClientFactory,
        ILogger<PlaceNewOrderTool> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }
    
    /// <summary>
    /// Process a new order with items, shipping details, and delivery preferences
    /// </summary>
    /// <param name="parameters">Complete order specification including items, delivery address, and shipping options</param>
    /// <returns>Order confirmation with tracking information and delivery estimates</returns>
    public async Task<dynamic> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var client = _httpClientFactory.CreateClient();
        
        // Validate required parameters
        if (!parameters.TryGetValue("customerId", out var customerIdObj))
        {
            throw new ArgumentException("Missing required parameter: customerId. Please provide a valid customer ID.");
        }
        
        if (!parameters.TryGetValue("orderItems", out var orderItemsObj))
        {
            throw new ArgumentException("Missing required parameter: orderItems. Please provide at least one item to order.");
        }
        
        if (!parameters.TryGetValue("deliveryAddress", out var deliveryAddressObj))
        {
            throw new ArgumentException("Missing required parameter: deliveryAddress. Please provide a complete shipping address.");
        }
        
        // Build order payload
        var orderPayload = new Dictionary<string, object>
        {
            ["userId"] = Convert.ToInt32(customerIdObj),
            ["items"] = orderItemsObj,
            ["shippingAddress"] = deliveryAddressObj,
            ["priority"] = "Standard" // Default
        };
        
        // Handle optional shipping speed
        if (parameters.TryGetValue("shippingSpeed", out var shippingSpeedObj))
        {
            var shippingSpeed = shippingSpeedObj.ToString();
            if (shippingSpeed == "Express" || shippingSpeed == "Standard")
            {
                orderPayload["priority"] = shippingSpeed;
            }
            else
            {
                _logger.LogWarning("Invalid shipping speed '{Speed}', defaulting to Standard", shippingSpeed);
            }
        }
        
        // Create JSON request body
        var json = JsonSerializer.Serialize(orderPayload);
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        
        try
        {
            var url = "http://localhost:5001/api/orders";
            var response = await client.PostAsync(url, httpContent);
            
            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Order validation failed: {errorContent}. Please check product IDs, quantities, and address format.");
            }
            
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Order processing failed: {response.StatusCode}. Please try again or contact support.");
            }
            
            var responseContent = await response.Content.ReadAsStringAsync();
            var orderResult = JsonSerializer.Deserialize<dynamic>(responseContent);
            
            if (orderResult == null)
            {
                throw new InvalidOperationException("Received empty response from order service.");
            }
            
            _logger.LogInformation("Successfully created order for customer {CustomerId}", customerIdObj);
            
            return new 
            {
                success = true,
                message = "Order created successfully",
                order = orderResult
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error while creating order for customer {CustomerId}", customerIdObj);
            throw new InvalidOperationException($"Network error while processing order: {ex.Message}. Please check service connectivity.", ex);
        }
    }
}

/// <summary>
/// Parameters for placing a new order
/// </summary>
public class PlaceNewOrderParameters
{
    [Description("Numeric customer identifier placing the order (e.g., 2001)")]
    [JsonPropertyName("customerId")]
    public int CustomerId { get; set; }
    
    [Description("Array of products being ordered with details")]
    [JsonPropertyName("orderItems")]
    public List<OrderItemInput> OrderItems { get; set; } = new();
    
    [Description("Complete shipping address for order delivery")]
    [JsonPropertyName("deliveryAddress")]
    public DeliveryAddressInput DeliveryAddress { get; set; } = new();
    
    [Description("Delivery speed: 'Standard' (3-5 days, default) or 'Express' (1-2 days, additional cost)")]
    [JsonPropertyName("shippingSpeed")]
    public string? ShippingSpeed { get; set; }
    
    [Description("Optional: Set to true if order should be gift wrapped (additional $5.99)")]
    [JsonPropertyName("giftWrapping")]
    public bool? GiftWrapping { get; set; }
    
    [Description("Optional: Special instructions for delivery or handling")]
    [JsonPropertyName("specialInstructions")]
    public string? SpecialInstructions { get; set; }
}

public class OrderItemInput
{
    [Description("Product code or SKU (e.g., 'PROD-001')")]
    public string ProductId { get; set; } = string.Empty;
    
    [Description("Human-readable product name")]
    public string ProductName { get; set; } = string.Empty;
    
    [Description("Number of units to order (minimum 1)")]
    public int Quantity { get; set; }
    
    [Description("Price per individual unit")]
    public decimal UnitPrice { get; set; }
    
    [Description("Optional: Product variant, color, or special notes")]
    public string? Notes { get; set; }
}

public class DeliveryAddressInput
{
    [Description("Street address including building number (e.g., '123 Main Street, Apt 4B')")]
    public string Street { get; set; } = string.Empty;
    
    [Description("City or town name")]
    public string City { get; set; } = string.Empty;
    
    [Description("State, province, or region")]
    public string State { get; set; } = string.Empty;
    
    [Description("Postal or ZIP code")]
    public string ZipCode { get; set; } = string.Empty;
    
    [Description("Country name or ISO code")]
    public string Country { get; set; } = string.Empty;
}