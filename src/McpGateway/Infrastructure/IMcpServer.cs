namespace McpGateway.Infrastructure;

public interface IMcpServer
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task<IServer> GetServerAsync();
}

public interface IServer
{
    Task<ProtocolMessage> HandleMessageAsync(ProtocolMessage message);
}