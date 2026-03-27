using System.Net.Sockets;
using System.IO;
using System.Text;

namespace Lukoil.Client.Services;

public sealed class TcpClientService(ILogService logService) : ITcpClientService
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public bool IsConnected => _client?.Connected == true;

    public async Task ConnectAsync(string host, int port, int timeoutMs, CancellationToken cancellationToken = default)
    {
        if (IsConnected)
        {
            return;
        }

        var client = new TcpClient();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeoutMs);

        try
        {
            await client.ConnectAsync(host, port, timeoutCts.Token);
            _client = client;
            _stream = client.GetStream();
            logService.Info($"Connected to {host}:{port}");
        }
        catch (OperationCanceledException)
        {
            client.Dispose();
            throw new TimeoutException($"Connection timeout to {host}:{port}.");
        }
        catch (SocketException ex)
        {
            client.Dispose();
            throw new IOException($"Connection failed ({ex.SocketErrorCode}).", ex);
        }
    }

    public Task DisconnectAsync()
    {
        try
        {
            _stream?.Dispose();
            _client?.Dispose();
            logService.Info("TCP disconnected");
        }
        finally
        {
            _stream = null;
            _client = null;
        }

        return Task.CompletedTask;
    }

    public async Task<string> SendCommandAsync(string command, int timeoutMs, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _stream is null)
        {
            throw new InvalidOperationException("Not connected to server.");
        }

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeoutMs);
            var ct = timeoutCts.Token;

            var request = Encoding.UTF8.GetBytes(command.Trim() + "\n");
            await _stream.WriteAsync(request, ct);
            await _stream.FlushAsync(ct);

            var buffer = new byte[4096];
            var sb = new StringBuilder();
            var lastDataAt = DateTime.UtcNow;

            while (!ct.IsCancellationRequested)
            {
                if (_stream.DataAvailable)
                {
                    var read = await _stream.ReadAsync(buffer, ct);
                    if (read == 0)
                    {
                        break;
                    }

                    sb.Append(Encoding.UTF8.GetString(buffer, 0, read));
                    lastDataAt = DateTime.UtcNow;
                    continue;
                }

                if (sb.Length > 0 && DateTime.UtcNow - lastDataAt > TimeSpan.FromMilliseconds(220))
                {
                    break;
                }

                await Task.Delay(25, ct);
            }

            if (sb.Length == 0)
            {
                throw new TimeoutException("Server did not return response in time.");
            }

            return sb.ToString().Trim();
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException("Request timeout exceeded.");
        }
        catch (IOException ex)
        {
            throw new IOException("Network stream error (possibly broken pipe).", ex);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
