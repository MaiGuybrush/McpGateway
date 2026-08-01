using Microsoft.AspNetCore.Mvc.Testing;
using WireMock.Server;
using WireMock.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace McpGateway.Tests;

public abstract class IntegrationTestBase : IDisposable
{
    protected WebApplicationFactory<Program> Factory { get; }
    protected WireMockServer MockOcelotServer { get; }
    protected HttpClient HttpClient { get; }
    
    protected IntegrationTestBase()
    {
        // 啟動 WireMock 伺服器來模擬 Ocelot API
        MockOcelotServer = WireMockServer.Start(new WireMockServerSettings
        {
            Port = 5001,
            UseSSL = false
        });
        
        // 配置 WebApplicationFactory
        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // 覆寫 HttpClient BaseAddress 以指向 WireMock
                    services.Configure<HttpClientOptions>(options =>
                    {
                        options.BaseAddress = new Uri(MockOcelotServer.Urls[0]);
                    });
                });
            });
        
        HttpClient = Factory.CreateClient();
    }
    
    public void Dispose()
    {
        HttpClient?.Dispose();
        Factory?.Dispose();
        MockOcelotServer?.Stop();
        MockOcelotServer?.Dispose();
    }
}