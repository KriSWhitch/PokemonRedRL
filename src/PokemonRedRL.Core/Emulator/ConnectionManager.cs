using System.Net.Sockets;
using PokemonRedRL.Core.Helpers;

namespace PokemonRedRL.Core.Emulator;

public class ConnectionManager : IDisposable
{
    private readonly NetworkConfig _config;
    private TcpClient _client;

    public NetworkStream Stream { get; set; }

    public ConnectionManager(NetworkConfig config) => _config = config;

    public void EnsureConnected()
    {
        if (_client?.Connected == true) return;

        int retries = 0;
        while (retries++ < _config.MaxRetries)
        {
            try
            {
                _client = new TcpClient();
                _client.Connect(_config.Host, _config.Port);
                Stream = _client.GetStream();
                Console.WriteLine($"Connected successfully with address {_config.Host}:{_config.Port}");
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connection attempt {retries} failed: {ex.Message}");
                Thread.Sleep(_config.RetryDelayMs);
            }
        }
        throw new TimeoutException("Connection failed");
    }

    public void Dispose()
    {
        Stream?.Dispose();
        _client?.Dispose();
    }
}