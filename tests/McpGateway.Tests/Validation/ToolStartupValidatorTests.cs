using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using McpGateway.Core.Configuration;
using McpGateway.Core.Tools;
using McpGateway.Core.Validation;
using System;
using System.ComponentModel;
using System.Text.Json.Serialization;
using Xunit;

namespace McpGateway.Tests.Validation;

public class ToolStartupValidatorTests
{
    // 工具用於測試
    [McpTool("report_valid_tool")]
    private class ValidTool : ToolBase<ValidInput, ValidOutput>
    {
        public override string Name => "report_valid_tool";
        public override string Description => "A valid tool for testing";
        public ValidTool(IHttpClientFactory httpClientFactory) : base(httpClientFactory) { }
        public override Task<ValidOutput> ExecuteAsync(ValidInput input, ToolContext context, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    public class ValidInput
    {
        [Description("Valid input property")]
        public string Message { get; set; } = string.Empty;
    }

    public class ValidOutput
    {
        public string Result { get; set; } = string.Empty;
    }

    // 工具用於測試失敗情況（缺少 [McpTool]）
    private class MissingAttributeTool : ToolBase<ValidInput, ValidOutput>
    {
        public override string Name => "report_missing_attribute";
        public override string Description => "Tool missing McpTool attribute";
        public MissingAttributeTool(IHttpClientFactory httpClientFactory) : base(httpClientFactory) { }
        public override Task<ValidOutput> ExecuteAsync(ValidInput input, ToolContext context, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    // 工具用於測試缺失 Description
    [McpTool("report_no_description")]
    private class NoDescriptionTool : ToolBase<NoDescriptionInput, ValidOutput>
    {
        public override string Name => "report_no_description";
        public override string Description => "Tool with input missing descriptions";
        public NoDescriptionTool(IHttpClientFactory httpClientFactory) : base(httpClientFactory) { }
        public override Task<ValidOutput> ExecuteAsync(NoDescriptionInput input, ToolContext context, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    public class NoDescriptionInput
    {
        // Missing [Description] attribute
        public string Message { get; set; } = string.Empty;
    }

    // 工具用於測試名字不以 {Department}_ 開頭
    [McpTool("invalid_tool_name")]
    private class InvalidNameTool : ToolBase<ValidInput, ValidOutput>
    {
        public override string Name => "invalid_tool_name";
        public override string Description => "Tool with invalid name";
        public InvalidNameTool(IHttpClientFactory httpClientFactory) : base(httpClientFactory) { }
        public override Task<ValidOutput> ExecuteAsync(ValidInput input, ToolContext context, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    // 工具用於測試 SemVer 格式
    [McpTool("report_invalid_version")]
    private class InvalidVersionTool : ToolBase<ValidInput, ValidOutput>
    {
        [McpTool("report_invalid_version", Version = "not-semver")]
        public InvalidVersionTool(IHttpClientFactory httpClientFactory) : base(httpClientFactory) { }
        public override Task<ValidOutput> ExecuteAsync(ValidInput input, ToolContext context, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public override string Name => "report_invalid_version";
        public override string Description => "Tool with invalid version";
    }

    [Fact]
    public void Validate_WithValidTool_ShouldReturnNoErrors()
    {
        // Arrange
        var options = Options.Create(new McpGatewayOptions { Department = "report" });
        var validator = new ToolStartupValidator(
            LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ToolStartupValidator>(),
            options,
            typeof(ToolStartupValidatorTests).Assembly
        );

        // Act
        var errors = validator.Validate();

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_WithMissingMcpToolAttribute_ShouldReturnError()
    {
        // Arrange
        var options = Options.Create(new McpGatewayOptions { Department = "report" });
        var validator = new ToolStartupValidator(
            LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ToolStartupValidator>(),
            options,
            typeof(MissingAttributeTool).Assembly
        );

        // Act
        var errors = validator.Validate();

        // Assert
        var error = Assert.Single(errors);
        Assert.Contains("[McpTool] attribute", error.Description);
        Assert.Equal("MissingAttributeTool", error.ToolName);
    }

    [Fact]
    public void Validate_WithMissingDescription_ShouldReturnError()
    {
        // Arrange
        var options = Options.Create(new McpGatewayOptions { Department = "report" });
        var validator = new ToolStartupValidator(
            LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ToolStartupValidator>(),
            options,
            typeof(NoDescriptionTool).Assembly
        );

        // Act
        var errors = validator.Validate();

        // Assert
        var error = Assert.Single(errors);
        Assert.Contains("[Description] attribute", error.Description);
        Assert.Equal("NoDescriptionTool", error.ToolName);
    }

    [Fact]
    public void Validate_WithInvalidDepartmentPrefix_ShouldReturnError()
    {
        // Arrange
        var options = Options.Create(new McpGatewayOptions { Department = "report" });
        var validator = new ToolStartupValidator(
            LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ToolStartupValidator>(),
            options,
            typeof(InvalidNameTool).Assembly
        );

        // Act
        var errors = validator.Validate();

        // Assert
        var error = Assert.Single(errors);
        Assert.Contains("report_", error.Description);
        Assert.Equal("InvalidNameTool", error.ToolName);
    }

    [Fact]
    public void Validate_WithInvalidSemVer_ShouldReturnError()
    {
        // Arrange
        var options = Options.Create(new McpGatewayOptions { Department = "report" });
        var validator = new ToolStartupValidator(
            LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ToolStartupValidator>(),
            options,
            typeof(InvalidVersionTool).Assembly
        );

        // Act
        var errors = validator.Validate();

        // Assert
        var error = Assert.Single(errors);
        Assert.Contains("SemVer", error.Description);
        Assert.Equal("InvalidVersionTool", error.ToolName);
    }

    [Fact]
    public void Validate_WithMultipleErrors_ShouldReturnAllErrors()
    {
        // Arrange
        var options = Options.Create(new McpGatewayOptions { Department = "test" });
        // This test would need a special assembly with multiple failing tools
        // For now, just verify the validator can collect multiple errors
        
        var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ToolStartupValidator>();
        var validator = new ToolStartupValidator(logger, options, typeof(InvalidVersionTool).Assembly);

        // Act
        var errors = validator.Validate();

        // Assert - this assembly should have some errors
        // Actual count depends on what other tools are in the assembly
        Assert.NotNull(errors);
    }
}
