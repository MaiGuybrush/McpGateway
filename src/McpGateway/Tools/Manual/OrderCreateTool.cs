using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using ModelContextProtocol.Types;

namespace McpGateway.Tools.Manual;

/// <summary>
/// 在系統中建立新的訂單，支援多種產品和配送選項
/// </summary>
[Description("在系統中建立新的訂單，支援多種產品和配送選項")]
[McpToolType]
public class PlaceNewOrderTool
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    
    public PlaceNewOrderTool(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    [Description("建立新的訂單，包含客戶資訊、產品清單和配送選項")]
    [McpTool("place_new_order")]
    public async Task<CallToolResponse> ExecuteAsync(
        //========== 客戶資訊 (扁平化) ==========
        [Description("客戶的唯一識別碼 (UUID 格式)。例如: 550e8400-e29b-41d4-a716-446655440000")]
        string customerId,
        
        [Description("訂單備註或特殊指示（選填，最多 500 字元）")]
        string? orderNotes = null,
        
        //========== 產品清單 ==========
        [Description("訂單產品清單，JSON 陣列格式。每個產品需包含: productId (string), quantity (int), unitPrice (decimal)。例如: [{\"productId\":\"PROD-001\",\"quantity\":2,\"unitPrice\":99.99}]")]
        string productsJson,
        
        //========== 配送資訊 (扁平化) ==========
        [Description("配送方式: standard (標準配送), express (快速配送), same_day (當日配送)")]
        string shippingMethod,
        
        [Description("收件人完整地址。例如: 台北市信義區信義路五段7號")]
        string shippingAddress,
        
        [Description("收件人姓名")]
        string recipientName,
        
        [Description("收件人電話號碼")]
        string recipientPhone,
        
        [Description("配送備註（例如: 放管理室、上樓等）")]
        string? shippingInstructions = null,
        
        [Description("期望的配送日期（YYYY-MM-DD 格式）。若無指定則依配送方式自動計算")]
        string? requestedDeliveryDate = null,
        
        //========== 付款資訊 ==========
        [Description("付款方式: credit_card (信用卡), bank_transfer (銀行轉帳), cod (貨到付款)")]
        string paymentMethod,
        
        [Description("是否使用 VIP 會員折扣（預設: false）")]
        bool vipDiscount = false,
        
        [Description("優惠券代碼（選填）")]
        string? couponCode = null)
    {
        try
        {
            //========== 參數驗證 ==========
            var validationResult = ValidateParameters(
                customerId, productsJson, shippingMethod, paymentMethod,
                shippingAddress, recipientName, recipientPhone
            );
            
            if (!validationResult.IsValid)
            {
                return CreateErrorResponse($"參數驗證失敗: {string.Join(", ", validationResult.Errors)}");
            }

            //========== 解析產品清單 ==========
            List<ProductItem> products;
            try
            {
                products = JsonSerializer.Deserialize<List<ProductItem>>(productsJson, _jsonOptions);
                if (products == null || !products.Any())
                {
                    return CreateErrorResponse("產品清單不能為空");
                }
                
                // 驗證每個產品
                foreach (var product in products)
                {
                    if (string.IsNullOrWhiteSpace(product.ProductId))
                        return CreateErrorResponse($"產品 ID 不能為空");
                    if (product.Quantity <= 0)
                        return CreateErrorResponse($"產品數量必須大於 0");
                    if (product.UnitPrice < 0)
                        return CreateErrorResponse($"產品單價不能為負數");
                }
            }
            catch (JsonException ex)
            {
                return CreateErrorResponse($"產品清單格式錯誤: {ex.Message}。請確保為有效的 JSON 陣列格式");
            }

            //========== 構建訂單請求 ==========
            var orderRequest = new
            {
                customerId,
                orderNotes,
                products,
                shipping = new
                {
                    method = shippingMethod,
                    address = shippingAddress,
                    recipientName,
                    recipientPhone,
                    instructions = shippingInstructions,
                    requestedDeliveryDate
                },
                payment = new
                {
                    method = paymentMethod,
                    vipDiscount,
                    couponCode
                }
            };

            //========== 發送 POST 請求 ==========
            var jsonContent = JsonSerializer.Serialize(orderRequest, _jsonOptions);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("/api/orders", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var orderData = JsonSerializer.Deserialize<JsonElement>(responseContent);
                var orderId = orderData.TryGetProperty("orderId", out var idProp) ? idProp.GetString() : "unknown";
                
                return new CallToolResponse(
                    content: [Content.CreateText($"訂單建立成功！訂單編號: {orderId}")],
                    isError: false,
                    metadata: new Dictionary<string, object>
                    {
                        ["order_id"] = orderId,
                        ["order_data"] = orderData,
                        ["status_code"] = (int)response.StatusCode
                    }
                );
            }

            //========== 處理錯誤情況 ==========
            return HandleApiError(response.StatusCode, responseContent);
        }
        catch (HttpRequestException ex)
        {
            return CreateErrorResponse($"網路錯誤: {ex.Message}。請檢查 API 連線狀態");
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"意外錯誤: {ex.Message}");
        }
    }

    private ValidationResult ValidateParameters(
        string customerId, string productsJson, string shippingMethod, string paymentMethod,
        string shippingAddress, string recipientName, string recipientPhone)
    {
        var errors = new List<string>();

        // 驗證 customerId
        if (!Guid.TryParse(customerId, out _))
            errors.Add("customerId 必須是有效的 UUID 格式");

        // 驗證 shippingMethod
        var validShippingMethods = new[] { "standard", "express", "same_day" };
        if (!validShippingMethods.Contains(shippingMethod))
            errors.Add($"shippingMethod 必須是: {string.Join(", ", validShippingMethods)}");

        // 驗證 paymentMethod
        var validPaymentMethods = new[] { "credit_card", "bank_transfer", "cod" };
        if (!validPaymentMethods.Contains(paymentMethod))
            errors.Add($"paymentMethod 必須是: {string.Join(", ", validPaymentMethods)}");

        // 驗證必填欄位
        if (string.IsNullOrWhiteSpace(shippingAddress))
            errors.Add("shippingAddress 不能為空");
        if (string.IsNullOrWhiteSpace(recipientName))
            errors.Add("recipientName 不能為空");
        if (string.IsNullOrWhiteSpace(recipientPhone))
            errors.Add("recipientPhone 不能為空");

        // 驗證 productsJson 不是空
        if (string.IsNullOrWhiteSpace(productsJson))
            errors.Add("productsJson 不能為空");

        return new ValidationResult { IsValid = !errors.Any(), Errors = errors };
    }

    private CallToolResponse HandleApiError(HttpStatusCode statusCode, string content)
    {
        return statusCode switch
        {
            HttpStatusCode.BadRequest => CreateErrorResponse($"請求參數錯誤: {content}"),
            HttpStatusCode.Unauthorized => CreateErrorResponse("認證失敗，請檢查 API 金鑰"),
            HttpStatusCode.Forbidden => CreateErrorResponse("權限不足，無法建立訂單"),
            HttpStatusCode.Conflict => CreateErrorResponse($"訂單衝突: {content}"),
            HttpStatusCode.InternalServerError => CreateErrorResponse($"API 伺服器錯誤: {content}"),
            HttpStatusCode.ServiceUnavailable => CreateErrorResponse("API 服務暫時無法使用，請稍後再試"),
            HttpStatusCode.GatewayTimeout => CreateErrorResponse("API 請求超時，服務可能過載"),
            _ => CreateErrorResponse($"API 請求失敗，狀態碼: {statusCode}。詳情: {content}")
        };
    }

    private CallToolResponse CreateErrorResponse(string message)
    {
        return new CallToolResponse(
            content: [Content.CreateText($"❌ 錯誤: {message}")],
            isError: true
        );
    }

    //========== 內部資料模型 ==========
    private record ProductItem
    {
        [JsonPropertyName("productId")]
        public string ProductId { get; set; } = string.Empty;
        
        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }
        
        [JsonPropertyName("unitPrice")]
        public decimal UnitPrice { get; set; }
    }

    private class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}

/// <summary>
/// 提供 MCP Tool 的範例使用方式
/// </summary>
public static class PlaceNewOrderExamples
{
    public static readonly Example[] Examples = new[]
    {
        new Example
        {
            Description = "基本訂單 - 標準配送 + 信用卡付款",
            Input = new Dictionary<string, object>
            {
                ["customerId"] = "550e8400-e29b-41d4-a716-446655440000",
                ["productsJson"] = "[{\"productId\":\"PROD-001\",\"quantity\":2,\"unitPrice\":99.99}]",
                ["shippingMethod"] = "standard",
                ["shippingAddress"] = "台北市信義區信義路五段7號",
                ["recipientName"] = "王小明",
                ["recipientPhone"] = "0912345678",
                ["paymentMethod"] = "credit_card"
            }
        },
        new Example
        {
            Description = "VIP 折扣訂單 - 快速配送",
            Input = new Dictionary<string, object>
            {
                ["customerId"] = "550e8400-e29b-41d4-a716-446655440000",
                ["productsJson"] = "[{\"productId\":\"PROD-001\",\"quantity\":1,\"unitPrice\":299.99}]",
                ["shippingMethod"] = "express",
                ["shippingAddress"] = "台北市中山區中山北路二段",
                ["recipientName"] = "李大華",
                ["recipientPhone"] = "0987654321",
                ["paymentMethod"] = "credit_card",
                ["vipDiscount"] = true,
                ["orderNotes"] = "VIP 客戶，請優先處理"
            }
        },
        new Example
        {
            Description = "多產品訂單 - 使用優惠券",
            Input = new Dictionary<string, object>
            {
                ["customerId"] = "550e8400-e29b-41d4-a716-446655440000",
                ["productsJson"] = "[{\"productId\":\"PROD-001\",\"quantity\":2,\"unitPrice\":49.99},{\"productId\":\"PROD-002\",\"quantity\":1,\"unitPrice\":129.99}]",
                ["shippingMethod"] = "standard",
                ["shippingAddress"] = "新北市板橋區文化路一段",
                ["recipientName"] = "陳美麗",
                ["recipientPhone"] = "0923456789",
                ["paymentMethod"] = "bank_transfer",
                ["couponCode"] = "SAVE2024"
            }
        }
    };
}