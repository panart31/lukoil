using Lukoil.Client.Models;

namespace Lukoil.Client.Services;

public interface ITcpClientService
{
    bool IsConnected { get; }
    Task ConnectAsync(string host, int port, int timeoutMs, CancellationToken cancellationToken = default);
    Task DisconnectAsync();
    Task<string> SendCommandAsync(string command, int timeoutMs, CancellationToken cancellationToken = default);
}
