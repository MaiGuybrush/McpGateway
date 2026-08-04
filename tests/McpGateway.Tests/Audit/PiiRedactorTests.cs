using System.Text.Json;
using McpGateway.Core.Audit;
using Xunit;

namespace McpGateway.Tests.Audit;

public class PiiRedactorTests
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void Redact_EmailField_FormatsCorrectly()
    {
        // Arrange
        var piiFields = new[] { "email", "customerEmail" };
        var redactor = new PiiRedactor(piiFields);
        
        var input = new Dictionary<string, object?>
        {
            ["email"] = "customer@example.com",
            ["customerEmail"] = "test.user@company.co.uk"
        };

        // Act
        var result = redactor.Redact(input);

        // Assert
        var resultDict = Assert.IsType<Dictionary<string, object?>>(result);
        Assert.Equal("c***@example.com", resultDict["email"]);
        Assert.Equal("t***@company.co.uk", resultDict["customerEmail"]);
    }

    [Fact]
    public void Redact_EmailInArray_FormatsCorrectly()
    {
        // Arrange
        var piiFields = new[] { "email" };
        var redactor = new PiiRedactor(piiFields);
        
        var input = new List<object>
        {
            new Dictionary<string, object?> { ["email"] = "user@domain.com" },
            new Dictionary<string, object?> { ["email"] = "admin@site.org" }
        };

        // Act
        var result = redactor.Redact(input);

        // Assert
        var resultList = Assert.IsType<List<object?>>(result);
        var item1 = Assert.IsType<Dictionary<string, object?>>(resultList[0]);
        var item2 = Assert.IsType<Dictionary<string, object?>>(resultList[1]);
        Assert.Equal("u***@domain.com", item1["email"]);
        Assert.Equal("a***@site.org", item2["email"]);
    }

    [Fact]
    public void Redact_NonEmailFields_MaskWithTwoChars()
    {
        // Arrange
        var piiFields = new[] { "customerName", "phone", "address", "ssn" };
        var redactor = new PiiRedactor(piiFields);
        
        var input = new Dictionary<string, object?>
        {
            ["customerName"] = "王小明",
            ["phone"] = "0912345678",
            ["address"] = "台北市信義區",
            ["ssn"] = "123456789"
        };

        // Act
        var result = redactor.Redact(input);

        // Assert
        var resultDict = Assert.IsType<Dictionary<string, object?>>(result);
        Assert.Equal("王小***", resultDict["customerName"]); // Unicode characters
        Assert.Equal("09***", resultDict["phone"]);
        Assert.Equal("台北***", resultDict["address"]);
        Assert.Equal("12***", resultDict["ssn"]);
    }

    [Fact]
    public void Redact_ShortValues_MaskCorrectly()
    {
        // Arrange
        var piiFields = new[] { "code", "pin" };
        var redactor = new PiiRedactor(piiFields);
        
        var input = new Dictionary<string, object?>
        {
            ["code"] = "AB",
            ["pin"] = "1"
        };

        // Act
        var result = redactor.Redact(input);

        // Assert
        var resultDict = Assert.IsType<Dictionary<string, object?>>(result);
        Assert.Equal("AB***", resultDict["code"]); // Uses all available chars
        Assert.Equal("1***", resultDict["pin"]);
    }

    [Fact]
    public void Redact_EmptyValues_HandleGracefully()
    {
        // Arrange
        var piiFields = new[] { "email", "name" };
        var redactor = new PiiRedactor(piiFields);
        
        var input = new Dictionary<string, object?>
        {
            ["email"] = "",
            ["name"] = null,
            ["address"] = "Valid Address"
        };

        // Act
        var result = redactor.Redact(input);

        // Assert
        var resultDict = Assert.IsType<Dictionary<string, object?>>(result);
        Assert.Equal(string.Empty, resultDict["email"]);
        Assert.Null(resultDict["name"]);
        Assert.Equal("Valid Address", resultDict["address"]); // Non-PII remains
    }

    [Fact]
    public void Redact_NestedObject_MasksRecursively()
    {
        // Arrange
        var piiFields = new[] { "email", "customerName", "address", "phone" };
        var redactor = new PiiRedactor(piiFields);
        
        var input = new Dictionary<string, object?>
        {
            ["user"] = new Dictionary<string, object?>
            {
                ["email"] = "nested@example.com",
                ["customerName"] = "李小華"
            },
            ["contacts"] = new List<object>
            {
                new Dictionary<string, object?>
                {
                    ["phone"] = "0922222222",
                    ["address"] = "新北市板橋區"
                }
            }
        };

        // Act
        var result = redactor.Redact(input);

        // Assert
        var resultDict = Assert.IsType<Dictionary<string, object?>>(result);
        
        var user = Assert.IsType<Dictionary<string, object?>>(resultDict["user"]);
        Assert.Equal("n***@example.com", user["email"]);
        Assert.Equal("李小***", user["customerName"]);
        
        var contacts = Assert.IsType<List<object?>>(resultDict["contacts"]);
        var contact = Assert.IsType<Dictionary<string, object?>>(contacts[0]);
        Assert.Equal("09***", contact["phone"]);
        Assert.Equal("新北***", contact["address"]);
    }

    [Fact]
    public void Redact_JsonElement_MasksCorrectly()
    {
        // Arrange
        var piiFields = new[] { "email", "customerName" };
        var redactor = new PiiRedactor(piiFields);
        
        var json = @"{""email"": ""test@example.com"", ""customerName"": ""張三"", ""age"": 30}";
        var jsonElement = JsonDocument.Parse(json).RootElement;

        // Act
        var result = redactor.Redact(jsonElement);

        // Assert
        var resultDict = Assert.IsType<Dictionary<string, object?>>(result);
        Assert.Equal("t***@example.com", resultDict["email"]);
        Assert.Equal("張三***", resultDict["customerName"]);
        Assert.Equal(30, resultDict["age"]); // Non-PII remains unmodified
    }

    [Fact]
    public void Redact_ComplexNestedStructure_HandlesGracefully()
    {
        // Arrange
        var piiFields = new[] { "email", "name", "phone" };
        var redactor = new PiiRedactor(piiFields);
        
        var input = new Dictionary<string, object?>
        {
            ["level1"] = new Dictionary<string, object?>
            {
                ["level2"] = new Dictionary<string, object?>
                {
                    ["email"] = "deep@nested.com",
                    ["name"] = "Deep Nested"
                },
                ["phone"] = "0933333333"
            },
            ["mixedArray"] = new List<object>
            {
                "notPII",
                new Dictionary<string, object?> { ["email"] = "arrayItem@example.com" },
                12345,
                true
            }
        };

        // Act
        var result = redactor.Redact(input);

        // Assert
        var resultDict = Assert.IsType<Dictionary<string, object?>>(result);
        
        var level1 = Assert.IsType<Dictionary<string, object?>>(resultDict["level1"]);
        var level2 = Assert.IsType<Dictionary<string, object?>>(level1["level2"]);
        Assert.Equal("d***@nested.com", level2["email"]);
        Assert.Equal("De***", level2["name"]);
        Assert.Equal("09***", level1["phone"]);
        
        var mixedArray = Assert.IsType<List<object?>>(resultDict["mixedArray"]);
        Assert.Equal("notPII", mixedArray[0]); // Non-PII string
        var arrayItem = Assert.IsType<Dictionary<string, object?>>(mixedArray[1]);
        Assert.Equal("a***@example.com", arrayItem["email"]);
        Assert.Equal(12345, mixedArray[2]); // Number unchanged
        Assert.Equal(true, mixedArray[3]); // Boolean unchanged
    }

    [Fact]
    public void Redactor_PiiFields_CaseInsensitive()
    {
        // Arrange
        var piiFields = new[] { "EMAIL", "CustomerName", "PHONE" };
        var redactor = new PiiRedactor(piiFields);
        
        var input = new Dictionary<string, object?>
        {
            ["email"] = "lowercase@example.com",    // lowercase
            ["customerName"] = "李四",               // camelCase
            ["CustomerName"] = "王五",               // PascalCase
            ["phone"] = "0944444444",
            ["EMAIL"] = "uppercase@example.com"
        };

        // Act
        var result = redactor.Redact(input);

        // Assert
        var resultDict = Assert.IsType<Dictionary<string, object?>>(result);
        Assert.Equal("l***@example.com", resultDict["email"]);
        Assert.Equal("李***", resultDict["customerName"]);
        Assert.Equal("王***", resultDict["CustomerName"]);
        Assert.Equal("09***", resultDict["phone"]);
        Assert.Equal("u***@example.com", resultDict["EMAIL"]);
    }

    [Fact]
    public void Redaction_Result_LogsWithoutOriginalPii()
    {
        // Arrange - This test verifies the critical requirement that original
        // PII values do not appear in any sink output
        var piiFields = new[] { "email", "ssn", "creditCard" };
        var redactor = new PiiRedactor(piiFields);
        
        var originalEmail = "customer@example.com";
        var originalSsn = "123456789";
        var originalCard = "1234567890123456";
        
        var input = new Dictionary<string, object?>
        {
            ["email"] = originalEmail,
            ["ssn"] = originalSsn,
            ["creditCard"] = originalCard,
            ["safeField"] = "public information"
        };

        // Act
        var result = redactor.Redact(input);
        var resultJson = JsonSerializer.Serialize(result, _jsonOptions);

        // Assert - Verify original PII values do NOT appear in serialized output
        Assert.DoesNotContain(originalEmail, resultJson);
        Assert.DoesNotContain(originalSsn, resultJson);
        Assert.DoesNotContain(originalCard, resultJson);
        
        // But non-PII values should be present
        Assert.Contains("public information", resultJson);
        
        // Verify masking patterns are present
        Assert.Contains("***", resultJson);
        Assert.Contains("c***@example.com", resultJson);
    }

    [Fact]
    public void EmailDetection_AlsoWorksForNonStandardFieldNames()
    {
        // Arrange - Email fields that don't follow naming conventions
        var piiFields = new[] { "user", "contact" }; // Not email-specific names
        var redactor = new PiiRedactor(piiFields);
        
        var input = new Dictionary<string, object?>
        {
            ["user"] = "admin@company.com",  // Contains @ symbol
            ["contact"] = "support@help.org"   // Contains @ symbol
        };

        // Act
        var result = redactor.Redact(input);

        // Assert - Should detect @ symbol and apply email masking
        var resultDict = Assert.IsType<Dictionary<string, object?>>(result);
        Assert.Equal("a***@company.com", resultDict["user"]);
        Assert.Equal("s***@help.org", resultDict["contact"]);
    }
}