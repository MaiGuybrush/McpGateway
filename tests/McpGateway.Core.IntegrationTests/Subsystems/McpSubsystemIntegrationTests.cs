using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using McpGateway.Core.Configuration;
using McpGateway.Core.Hosting;
using McpGateway.Core.Subsystems;
using Xunit;

namespace McpGateway.Core.IntegrationTests.Subsystems;

public class McpSubsystemIntegrationTests : IAsyncLifetime
{
    private WebApplication? _app;
    private HttpClient? _client;

    [McpTool("mfg_mes_query_lot")]
    public class MesIntegrationTool
    {
        public string QueryLot(string lotId) => $"Lot: {lotId}";
    }

    [McpTool("mfg_wms_query_stock")]
    public class WmsIntegrationTool
    {
        public string QueryStock(string itemId) => $"Stock for: {itemId}";
    }

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.Configure<McpGatewayOptions>(options =>
        {
            options.Department = "mfg";
            options.RoutePrefix = "/mfg/mcp";
            options.EnableHealthChecks = true;
            options.Auth = new AuthOptions { Enabled = false }; // Disable auth for isolation tests
        });

        // Add Core gateway
        builder.Services.AddMcpGateway();

        // Add MES Subsystem
        builder.Services.AddMcpSubsystem("mes", subsystem =>
        {
            subsystem.WithTools<MesIntegrationTool>();
        });

        // Add WMS Subsystem
        builder.Services.AddMcpSubsystem("wms", subsystem =>
        {
            subsystem.WithTools<WmsIntegrationTool>();
        });

        _app = builder.Build();
        _app.MapMcpGateway();

        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        if (_client != null)
        {
            _client.Dispose();
        }
        if (_app != null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    [Fact]
    public async Task SessionOptions_FiltersToolsPerSubsystemPath()
    {
        // Assert registry was populated
        var registry = _app!.Services.GetRequiredService<IMcpSubsystemRegistry>();
        Assert.True(registry.ContainsSubsystem("mes"));
        Assert.True(registry.ContainsSubsystem("wms"));

        // Direct verification of ConfigureSessionOptions execution logic
        var transportOptions = _app.Services.GetRequiredService<IOptions<ModelContextProtocol.AspNetCore.HttpServerTransportOptions>>().Value;
        Assert.NotNull(transportOptions.ConfigureSessionOptions);

        // 1. Simulate request to /mfg/mes/mcp
        var httpContextMes = new Microsoft.AspNetCore.Http.DefaultHttpContext { RequestServices = _app.Services };
        httpContextMes.Request.Path = "/mfg/mes/mcp";

        var mcpOptionsMes = new McpServerOptions();
        // Populate with all registered tools
        mcpOptionsMes.ToolCollection = new McpServerPrimitiveCollection<McpServerTool>(StringComparer.OrdinalIgnoreCase)
        {
            McpServerTool.Create((Func<string, string>)(x => x), new McpServerToolCreateOptions { Name = "mfg_mes_query_lot" }),
            McpServerTool.Create((Func<string, string>)(x => x), new McpServerToolCreateOptions { Name = "mfg_wms_query_stock" })
        };

        await transportOptions.ConfigureSessionOptions!(httpContextMes, mcpOptionsMes, default);

        // Mes session must only contain mes tools
        Assert.Single(mcpOptionsMes.ToolCollection);
        Assert.Contains("mfg_mes_query_lot", mcpOptionsMes.ToolCollection.PrimitiveNames);
        Assert.DoesNotContain("mfg_wms_query_stock", mcpOptionsMes.ToolCollection.PrimitiveNames);

        // 2. Simulate request to /mfg/wms/mcp
        var httpContextWms = new Microsoft.AspNetCore.Http.DefaultHttpContext { RequestServices = _app.Services };
        httpContextWms.Request.Path = "/mfg/wms/mcp";

        var mcpOptionsWms = new McpServerOptions();
        mcpOptionsWms.ToolCollection = new McpServerPrimitiveCollection<McpServerTool>(StringComparer.OrdinalIgnoreCase)
        {
            McpServerTool.Create((Func<string, string>)(x => x), new McpServerToolCreateOptions { Name = "mfg_mes_query_lot" }),
            McpServerTool.Create((Func<string, string>)(x => x), new McpServerToolCreateOptions { Name = "mfg_wms_query_stock" })
        };

        await transportOptions.ConfigureSessionOptions!(httpContextWms, mcpOptionsWms, default);

        // Wms session must only contain wms tools
        Assert.Single(mcpOptionsWms.ToolCollection);
        Assert.Contains("mfg_wms_query_stock", mcpOptionsWms.ToolCollection.PrimitiveNames);
        Assert.DoesNotContain("mfg_mes_query_lot", mcpOptionsWms.ToolCollection.PrimitiveNames);

        // 3. Simulate request to unregistered subsystem /mfg/unknown/mcp
        var httpContextUnknown = new Microsoft.AspNetCore.Http.DefaultHttpContext { RequestServices = _app.Services };
        httpContextUnknown.Request.Path = "/mfg/unknown/mcp";

        var mcpOptionsUnknown = new McpServerOptions();
        mcpOptionsUnknown.ToolCollection = new McpServerPrimitiveCollection<McpServerTool>(StringComparer.OrdinalIgnoreCase)
        {
            McpServerTool.Create((Func<string, string>)(x => x), new McpServerToolCreateOptions { Name = "mfg_mes_query_lot" }),
            McpServerTool.Create((Func<string, string>)(x => x), new McpServerToolCreateOptions { Name = "mfg_wms_query_stock" })
        };

        await transportOptions.ConfigureSessionOptions!(httpContextUnknown, mcpOptionsUnknown, default);

        // Unknown subsystem must contain 0 tools (strict isolation)
        Assert.Empty(mcpOptionsUnknown.ToolCollection);
    }

    [Fact]
    public async Task HttpEndpoint_SubsystemRoutesAreMappedAndAccessible()
    {
        // Verify both /mfg/mes/mcp and /mfg/wms/mcp respond (not 404)
        var responseMes = await _client!.GetAsync("/mfg/mes/mcp");
        var responseWms = await _client!.GetAsync("/mfg/wms/mcp");

        // ModelContextProtocol endpoints return 405 Method Not Allowed on GET (expects POST), which confirms endpoint is mapped!
        Assert.NotEqual(System.Net.HttpStatusCode.NotFound, responseMes.StatusCode);
        Assert.NotEqual(System.Net.HttpStatusCode.NotFound, responseWms.StatusCode);
    }
}
