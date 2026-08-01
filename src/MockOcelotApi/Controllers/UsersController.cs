using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

namespace MockOcelotApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    [HttpGet("{id}")]
    public IActionResult GetUser(string id, [FromQuery] string? fields = "all", [FromQuery] bool includeDeleted = false)
    {
        return Ok(new
        {
            id,
            name = "Test User",
            email = "test@example.com",
            status = "active",
            createdAt = "2024-01-01T00:00:00Z",
            fields,
            includeDeleted
        });
    }
}

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    [HttpPost]
    public IActionResult CreateOrder([FromBody] CreateOrderRequest request)
    {
        return Ok(new
        {
            orderId = Guid.NewGuid().ToString(),
            status = "created",
            timestamp = DateTime.UtcNow,
            request
        });
    }
}

public class CreateOrderRequest
{
    public string CustomerId { get; set; } = string.Empty;
    public List<ProductItem> Products { get; set; } = new();
    public ShippingInfo Shipping { get; set; } = new();
    public PaymentInfo Payment { get; set; } = new();
}

public class ProductItem
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class ShippingInfo
{
    public string Method { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public string RecipientPhone { get; set; } = string.Empty;
}

public class PaymentInfo
{
    public string Method { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TWD";
}