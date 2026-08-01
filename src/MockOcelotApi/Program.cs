using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Mock Ocelot API", Version = "v1" });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// GET /api/users/{id}
app.MapGet("/api/users/{id}", (int id, [FromQuery] bool includeDetails) =>
{
    var user = new UserDto
    {
        Id = id,
        Name = $"User {id}",
        Email = $"user{id}@example.com",
        Details = includeDetails ? new UserDetails
        {
            CreatedAt = DateTime.UtcNow.AddDays(-id),
            LastLogin = DateTime.UtcNow.AddHours(-id),
            Status = id % 2 == 0 ? "Active" : "Inactive"
        } : null
    };
    
    return Results.Ok(user);
})
.WithName("GetUser")
.WithOpenApi(operation =>
{
    operation.Summary = "Get user information by ID";
    operation.Description = "Retrieves detailed information about a specific user.";
    operation.Parameters[0].Description = "Unique identifier of the user";
    return operation;
});

// POST /api/orders
app.MapPost("/api/orders", async ([FromBody] OrderRequest request) =>
{
    if (request.Items == null || request.Items.Count == 0)
    {
        return Results.BadRequest(new { error = "Order must contain at least one item" });
    }

    var orderId = Guid.NewGuid().ToString();
    var total = request.Items.Sum(item => item.Quantity * item.UnitPrice);
    
    var order = new OrderDto
    {
        OrderId = orderId,
        Status = "Created",
        Total = total,
        CreatedAt = DateTime.UtcNow,
        EstimatedDelivery = request.Priority == "Express" 
            ? DateTime.UtcNow.AddDays(1) 
            : DateTime.UtcNow.AddDays(3)
    };
    
    return Results.Ok(order);
})
.WithName("CreateOrder")
.WithOpenApi(operation =>
{
    operation.Summary = "Create a new order";
    operation.Description = "Creates a new order with items and shipping information.";
    return operation;
});

app.Run();

// DTOs
public class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserDetails? Details { get; set; }
}

public class UserDetails
{
    public DateTime CreatedAt { get; set; }
    public DateTime LastLogin { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class OrderRequest
{
    public int UserId { get; set; }
    public List<OrderItem> Items { get; set; } = new();
    public ShippingAddress ShippingAddress { get; set; } = new();
    public string Priority { get; set; } = "Standard"; // Standard, Express
}

public class OrderItem
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? Notes { get; set; }
}

public class ShippingAddress
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public class OrderDto
{
    public string OrderId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime EstimatedDelivery { get; set; }
}