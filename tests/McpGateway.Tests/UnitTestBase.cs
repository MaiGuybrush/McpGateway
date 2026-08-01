using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace McpGateway.Tests;

public abstract class UnitTestBase
{
    protected IServiceProvider ServiceProvider { get; }
    
    protected Mock<ILogger<T>> CreateLoggerMock<T>()
    {
        return new Mock<ILogger<T>>();
    }
    
    protected UnitTestBase()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();
    }
    
    protected virtual void ConfigureServices(IServiceCollection services)
    {
        // 基礎服務配置
        services.AddLogging(builder => builder.AddConsole());
    }
}