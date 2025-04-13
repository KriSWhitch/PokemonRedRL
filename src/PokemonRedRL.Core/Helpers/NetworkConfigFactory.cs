using System.Net.Sockets;
using System.Net;
using System.Text;

namespace PokemonRedRL.Core.Helpers;

public class NetworkConfigFactory
{
    private readonly int _basePort;
    private readonly int _maxAttempts;
    private int _currentPort;
    private readonly object _portLock = new();

    public NetworkConfigFactory(int basePort = 12345, int maxAttempts = 60)
    {
        _basePort = basePort;
        _maxAttempts = maxAttempts;
        _currentPort = basePort - 1;
    }

    public NetworkConfig Create()
    {
        lock (_portLock)
        {
            int attempts = 0;
            while (attempts++ < _maxAttempts)
            {
                _currentPort = (_currentPort + 1) % (_basePort + _maxAttempts);
                if (IsEmulatorReady(_currentPort))
                {
                    return new NetworkConfig
                    {
                        Host = "127.0.0.1",
                        Port = _currentPort,
                        TimeoutMs = 5000,
                        MaxRetries = 5
                    };
                }
            }
            throw new InvalidOperationException($"No active mGBA instances found in range {_basePort}-{_currentPort}");
        }
    }

    private bool IsEmulatorReady(int port)
    {
        try
        {
            using var client = new TcpClient();
            var result = client.BeginConnect(IPAddress.Loopback, port, null, null);

            // Таймаут подключения 500 мс
            bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(500));
            client.EndConnect(result);

            if (success)
            {
                // Проверяем работоспособность через ping-команду
                var stream = client.GetStream();
                byte[] pingCmd = Encoding.ASCII.GetBytes("ping\n");
                stream.Write(pingCmd, 0, pingCmd.Length);

                byte[] buffer = new byte[256];
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                return Encoding.ASCII.GetString(buffer, 0, bytesRead).Trim() == "pong";
            }
            return false;
        }
        catch
        {
            return false;
        }
    }
}