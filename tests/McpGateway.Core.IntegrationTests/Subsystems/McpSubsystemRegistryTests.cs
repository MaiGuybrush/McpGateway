using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using McpGateway.Core.Hosting;
using McpGateway.Core.Subsystems;
using Xunit;

namespace McpGateway.Core.IntegrationTests.Subsystems;

public class McpSubsystemRegistryTests
{
    [McpTool("mfg_mes_query_lot")]
    private class DummyMesTool
    {
    }

    [McpTool("mfg_mes_defect_report")]
    private class DummyMesDefectTool
    {
    }

    [McpTool("mfg_wms_query_stock")]
    private class DummyWmsTool
    {
    }

    [Fact]
    public void Register_ExtractsAttributesAndStoresSubsystem()
    {
        var registry = new McpSubsystemRegistry();
        var reg = new McpSubsystemRegistration("mes", new[] { typeof(DummyMesTool) }, new[] { "mfg_mes_custom_action" });

        registry.Register(reg);

        Assert.True(registry.ContainsSubsystem("mes"));
        Assert.True(registry.ContainsSubsystem("MES")); // Case-insensitive
        Assert.False(registry.ContainsSubsystem("wms"));

        Assert.True(registry.TryGetSubsystem("mes", out var retrieved));
        Assert.NotNull(retrieved);
        Assert.Equal("mes", retrieved!.SubsystemName);
        Assert.Contains(typeof(DummyMesTool), retrieved.ToolTypes);
        Assert.Contains("mfg_mes_query_lot", retrieved.AllowedToolNames);
        Assert.Contains("mfg_mes_custom_action", retrieved.AllowedToolNames);
    }

    [Theory]
    [InlineData("mes", "mfg_mes_query_lot", true)]
    [InlineData("mes", "MFG_MES_QUERY_LOT", true)]
    [InlineData("mes", "mfg_wms_query_stock", false)]
    [InlineData("wms", "mfg_wms_query_stock", true)]
    [InlineData("wms", "mfg_mes_query_lot", false)]
    public void IsToolAuthorized_StrictExplicitWhitelist_ValidatesProperly(string subsystem, string toolName, bool expected)
    {
        var whitelist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            subsystem == "mes" ? "mfg_mes_query_lot" : "mfg_wms_query_stock"
        };
        var isAuth = McpSubsystemRegistry.IsToolAuthorized(subsystem, toolName, whitelist);
        Assert.Equal(expected, isAuth);
    }

    [Fact]
    public void IsToolAuthorized_WithoutExplicitWhitelist_ReturnsFalse()
    {
        var isAuth = McpSubsystemRegistry.IsToolAuthorized("mes", "mfg_mes_query_lot", explicitAllowedNames: null);
        Assert.False(isAuth);
    }

    [Fact]
    public void FilterToolsForSubsystem_FiltersToolCollectionBySubsystemWhitelist()
    {
        var registry = new McpSubsystemRegistry();
        registry.Register(new McpSubsystemRegistration("mes", new[] { typeof(DummyMesTool), typeof(DummyMesDefectTool) }));
        registry.Register(new McpSubsystemRegistration("wms", new[] { typeof(DummyWmsTool) }));

        // Create tools
        var tool1 = McpServerTool.Create(
            (Func<string, string>)(x => x),
            new McpServerToolCreateOptions { Name = "mfg_mes_query_lot" });

        var tool2 = McpServerTool.Create(
            (Func<string, string>)(x => x),
            new McpServerToolCreateOptions { Name = "mfg_mes_defect_report" });

        var tool3 = McpServerTool.Create(
            (Func<string, string>)(x => x),
            new McpServerToolCreateOptions { Name = "mfg_wms_query_stock" });

        var allTools = new McpServerPrimitiveCollection<McpServerTool>(StringComparer.OrdinalIgnoreCase);
        allTools.Add(tool1);
        allTools.Add(tool2);
        allTools.Add(tool3);

        // Act 1: Filter for MES
        var mesTools = registry.FilterToolsForSubsystem("mes", allTools);
        Assert.Equal(2, mesTools.Count);
        Assert.Contains("mfg_mes_query_lot", mesTools.PrimitiveNames);
        Assert.Contains("mfg_mes_defect_report", mesTools.PrimitiveNames);
        Assert.DoesNotContain("mfg_wms_query_stock", mesTools.PrimitiveNames);

        // Act 2: Filter for WMS
        var wmsTools = registry.FilterToolsForSubsystem("wms", allTools);
        Assert.Single(wmsTools);
        Assert.Contains("mfg_wms_query_stock", wmsTools.PrimitiveNames);
        Assert.DoesNotContain("mfg_mes_query_lot", wmsTools.PrimitiveNames);

        // Act 3: Filter for unregistered subsystem (strict isolation)
        var unknownTools = registry.FilterToolsForSubsystem("unregistered", allTools);
        Assert.Empty(unknownTools);
    }

    [Fact]
    public void AddMcpSubsystem_FluentApi_RegistersToDiProperly()
    {
        var services = new ServiceCollection();

        services.AddMcpSubsystem("mes", subsystem =>
        {
            subsystem.WithTools<DummyMesTool>()
                     .WithToolNames("mfg_mes_extra");
        });

        services.AddMcpSubsystem("wms", subsystem =>
        {
            subsystem.WithTools<DummyWmsTool>();
        });

        var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<IMcpSubsystemRegistry>();

        Assert.NotNull(registry);
        Assert.True(registry.ContainsSubsystem("mes"));
        Assert.True(registry.ContainsSubsystem("wms"));

        var mesAllowed = registry.GetAllowedToolNames("mes");
        Assert.Contains("mfg_mes_query_lot", mesAllowed);
        Assert.Contains("mfg_mes_extra", mesAllowed);
        Assert.DoesNotContain("mfg_wms_query_stock", mesAllowed);
    }
}
