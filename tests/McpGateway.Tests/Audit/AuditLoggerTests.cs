using System.Text.Json;
using McpGateway.Core.Audit;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace McpGateway.Tests.Audit;

public class AuditLoggerTests
{
    private readonly Mock<ILogger<AuditLogger>> _mockLogger;
    private readonly JsonSerializerOptions _jsonOptions;

    public AuditLoggerTests()
    {
        _mockLogger = new Mock<ILogger<AuditLogger>>();
        _jsonOptions = new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    [Fact]
    public void LogToolInvocation_ConsoleSink_FormatsCorrectly()
    {
        // Arrange
        var piiFields = new[] { "email", "customerName" };
        var redactor = new PiiRedactor(piiFields);
        var auditLogger = new AuditLogger(
            _mockLogger.Object, 
            redactor, 
            "console",
            "test-dept",
            "1.0.0",
            _jsonOptions);

        var log = new ToolInvocationAuditLog
        {
            Timestamp = DateTimeOffset.UtcNow,
            AgentId = "agent-123",
            ToolName = "test_tool",
            Department = "test-dept",
            CoreVersion = "1.0.0",
            CorrelationId = "corr-456",
            Parameters = new Dictionary<string, object?>
            {
                ["email"] = "test@example.com",
                ["customerName"] = "張三",
                ["safeField"] = "safe-value"
            },
            Success = true,
            DurationMs = 150
        };

        // Act
        auditLogger.LogToolInvocation(log);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Tool=test_tool")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()!),
            Times.Once);
    }

    [Fact]
    public void LogToolInvocation_FileSink_OutputsJson()
    {
        // Arrange
        var piiFields = new[] { "email" };
        var redactor = new PiiRedactor(piiFields);
        var auditLogger = new AuditLogger(
            _mockLogger.Object, 
            redactor, 
            "file",
            "finance",
            "2.0.0",
            _jsonOptions);

        var log = new ToolInvocationAuditLog
        {
            Timestamp = DateTimeOffset.UtcNow,
            AgentId = "finance-agent",
            ToolName = "report_generate",
            ToolVersion = "1.0",
            Department = "finance",
            CoreVersion = "2.0.0",
            CorrelationId = "corr-789",
            Parameters = new Dictionary<string, object?>
            {
                ["email"] = "finance@company.com",
                ["reportType"] = "quarterly"
            },
            Success = false,
            HttpStatusCode = 500,
            DurationMs = 2500,
            ErrorMessage = "Downstream service failed"
        };

        // Act
        auditLogger.LogToolInvocation(log);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("AUDIT:")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()!),
            Times.Once);
    }

    [Fact]
    public void LogToolInvocation_ApplicationInsightsSink_OutputsStructured()
    {
        // Arrange
        var piiFields = new[] { "email", "ssn" };
        var redactor = new PiiRedactor(piiFields);
        var auditLogger = new AuditLogger(
            _mockLogger.Object, 
            redactor, 
            "ApplicationInsights",
            "hr",
            "1.5.0",
            _jsonOptions);

        var log = new ToolInvocationAuditLog
        {
            Timestamp = DateTimeOffset.UtcNow,
            AgentId = "hr-bot-1",
            ToolName = "emp_lookup",
            ToolVersion = "2.1",
            Department = "hr",
            CoreVersion = "1.5.0",
            CorrelationId = "corr-101112",
            Parameters = new Dictionary<string, object?>
            {
                ["email"] = "employee@company.com",
                ["ssn"] = "123456789"
            },
            Success = true,
            DurationMs = 55
        };

        // Act
        auditLogger.LogToolInvocation(log);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("ToolInvocation:")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()!),
            Times.Once);
    }

    [Fact]
    public void LogToolInvocation_InvalidSink_FallsBackToConsole()
    {
        // Arrange
        var piiFields = new[] { "name" };
        var redactor = new PiiRedactor(piiFields);
        var auditLogger = new AuditLogger(
            _mockLogger.Object, 
            redactor, 
            "invalid-sink",
            "marketing",
            "1.0.0",
            _jsonOptions);

        var log = new ToolInvocationAuditLog
        {
            Timestamp = DateTimeOffset.UtcNow,
            AgentId = "marketing-agent",
            ToolName = "campaign_create",
            Department = "marketing",
            CoreVersion = "1.0.0",
            CorrelationId = "corr-test",
            Parameters = new Dictionary<string, object?>
            {
                ["name"] = "Confidential Campaign"
            },
            Success = true,
            DurationMs = 100
        };

        // Act
        auditLogger.LogToolInvocation(log);

        // Assert - Should fall back to console logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Tool=")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()!),
            Times.Once);
    }

    [Fact]
    public void LogToolInvocation_PiiRedaction_HappensBeforeLogging()
    {
        // Arrange - Test that PII is redacted before reaching the sink
        var piiFields = new[] { "creditCard", "email", "phone" };
        var redactor = new PiiRedactor(piiFields);
        var auditLogger = new AuditLogger(
            _mockLogger.Object, 
            redactor, 
            "file",
            "sales",
            "3.0.0",
            _jsonOptions);

        var originalCard = "4111111111111111";
        var originalEmail = "vip@company.com";
        var originalPhone = "0955555555";

        var log = new ToolInvocationAuditLog
        {
            Timestamp = DateTimeOffset.UtcNow,
            AgentId = "sales-agent",
            ToolName = "process_payment",
            Department = "sales",
            CoreVersion = "3.0.0",
            CorrelationId = "corr-payment",
            Parameters = new Dictionary<string, object?>
            {
                ["creditCard"] = originalCard,
                ["email"] = originalEmail,
                ["phone"] = originalPhone
            },
            Success = true,
            DurationMs = 500
        };

        string? capturedLogMessage = null;

        _mockLogger
            .Setup(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()!))
            .Callback<IInvocation>(invocation =>
            {
                var state = invocation.Arguments[2];
                capturedLogMessage = state?.ToString();
            });

        // Act
        auditLogger.LogToolInvocation(log);

        // Assert
        Assert.NotNull(capturedLogMessage);
        
        // Verify original PII values do NOT appear in the log message
        Assert.DoesNotContain(originalCard, capturedLogMessage);
        Assert.DoesNotContain(originalEmail, capturedLogMessage);
        Assert.DoesNotContain(originalPhone, capturedLogMessage);
        
        // But masked values DO appear
        Assert.Contains("***", capturedLogMessage);
    }

    [Fact]
    public void ToolInvocationAuditLog_AllFields_Present()
    {
        // Arrange & Act
        var log = new ToolInvocationAuditLog
        {
            Timestamp = DateTimeOffset.UtcNow,
            AgentId = "test-agent",
            ToolName = "test_tool",
            ToolVersion = "1.0.0",
            Department = "test",
            CoreVersion = "2.0.0",
            CorrelationId = "test-corr-123",
            Parameters = new Dictionary<string, object?> { ["key"] = "value" },
            Success = true,
            HttpStatusCode = 200,
            DurationMs = 100,
            ErrorMessage = null
        };

        // Assert - All required fields are present and set
        Assert.NotEqual(default, log.Timestamp);
        Assert.NotNull(log.AgentId);
        Assert.NotNull(log.ToolName);
        Assert.NotNull(log.Department);
        Assert.NotNull(log.CoreVersion);
        Assert.NotNull(log.CorrelationId);
        Assert.NotNull(log.Parameters);
        Assert.True(log.Success);
        Assert.Equal(200, log.HttpStatusCode);
        Assert.Equal(100, log.DurationMs);
        Assert.Null(log.ErrorMessage);
    }

    [Fact]
    public void ToolInvocationAuditLog_Serialization_IncludesAllFields()
    {
        // Arrange
        var log = new ToolInvocationAuditLog
        {
            Timestamp = new DateTimeOffset(2026, 8, 3, 10, 30, 0, TimeSpan.Zero),
            AgentId = "agent-999",
            ToolName = "full_test",
            ToolVersion = "2.5.0",
            Department = "finance",
            CoreVersion = "3.1.0",
            CorrelationId = "corr-serialization-test",
            Parameters = new Dictionary<string, object?>
            {
                ["param1"] = "value1",
                ["param2"] = 42,
                ["param3"] = true
            },
            Success = false,
            HttpStatusCode = 404,
            DurationMs = 1250,
            ErrorMessage = "Resource not found"
        };

        // Act
        var json = JsonSerializer.Serialize(log, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.True(deserialized.ContainsKey("timestamp"));
        Assert.True(deserialized.ContainsKey("agentId"));
        Assert.True(deserialized.ContainsKey("toolName"));
        Assert.True(deserialized.ContainsKey("toolVersion"));
        Assert.True(deserialized.ContainsKey("department"));
        Assert.True(deserialized.ContainsKey("coreVersion"));
        Assert.True(deserialized.ContainsKey("correlationId"));
        Assert.True(deserialized.ContainsKey("parameters"));
        Assert.True(deserialized.ContainsKey("success"));
        Assert.True(deserialized.ContainsKey("httpStatusCode"));
        Assert.True(deserialized.ContainsKey("durationMs"));
        Assert.True(deserialized.ContainsKey("errorMessage"));
    }
}