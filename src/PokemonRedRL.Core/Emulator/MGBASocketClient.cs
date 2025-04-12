using System;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using PokemonRedRL.Core.Enums;

namespace PokemonRedRL.Core.Emulator;

public class MGBASocketClient : IDisposable
{
    private const int TIMEOUT = 5000; // 5 секунд
    private const int RETRY_DELAY = 1000; // 1 секунда
    private const int MAX_RETRIES = 60;

    private readonly string _host;
    private readonly int _port;
    private TcpClient _client;
    private NetworkStream _stream;
    private readonly object _lock = new();

    public MGBASocketClient(string host = "127.0.0.1", int port = 12345)
    {
        _host = host;
        _port = port;
    }

    public void Connect()
    {
        int retries = 0;
        while (retries < MAX_RETRIES)
        {
            try
            {
                _client = new TcpClient();
                _client.ReceiveTimeout = TIMEOUT;
                _client.SendTimeout = TIMEOUT;
                _client.Connect(_host, _port);
                _stream = _client.GetStream();

                // Проверка соединения
                var response = SendCommandInternal("ping");
                if (response == "pong")
                {
                    Console.WriteLine("Connected successfully");
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connection attempt {retries + 1} failed: {ex.Message}");
                Dispose();
            }

            retries++;
            if (retries < MAX_RETRIES)
            {
                Thread.Sleep(RETRY_DELAY);
            }
        }

        throw new Exception($"Failed to connect after {MAX_RETRIES} attempts");
    }

    private string SendCommandInternal(string cmd, bool allowRetry = true)
    {
        lock (_lock)
        {
            int attempts = allowRetry ? 2 : 1;

            for (int i = 0; i < attempts; i++)
            {
                try
                {
                    // Проверка соединения
                    if (_client == null || !_client.Connected)
                    {
                        if (i == 0 && allowRetry)
                        {
                            Console.WriteLine("Reconnecting...");
                            Dispose();
                            Connect();
                            continue;
                        }
                        throw new Exception("Not connected");
                    }

                    // Отправка команды
                    byte[] cmdBytes = Encoding.ASCII.GetBytes(cmd + "\n");
                    _stream.Write(cmdBytes, 0, cmdBytes.Length);

                    // Чтение ответа
                    var buffer = new byte[1024];
                    var response = new StringBuilder();

                    while (true)
                    {
                        int bytesRead = _stream.Read(buffer, 0, buffer.Length);
                        response.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));

                        if (response.ToString().EndsWith("\n"))
                            break;

                        if (response.Length > 4096)
                            throw new Exception("Response too long");
                    }

                    return response.ToString().Trim();
                }
                catch (Exception ex)
                {
                    if (i == 0 && allowRetry)
                    {
                        Console.WriteLine($"Command failed, retrying... Error: {ex.Message}");
                        Dispose();
                        Connect();
                        continue;
                    }

                    Dispose();
                    throw new Exception($"Command '{cmd}' failed: {ex.Message}");
                }
            }

            throw new Exception("Unexpected error in command execution");
        }
    }

    public void SendButtonCommand(Buttons button)
    {
        string response = SendCommandInternal($"{button}");
        if (response != "OK")
            throw new Exception($"Unexpected response: {response}");
    }

    public GameStateResponse GetGameState()
    {
        string jsonResponse = SendCommandInternal("get_state");

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            };

            var state = JsonSerializer.Deserialize<GameStateResponse>(jsonResponse, options);

            if (state == null)
                throw new Exception("Empty response from emulator");

            return state;
        }
        catch (JsonException ex)
        {
            throw new Exception($"JSON parsing error: {ex.Message}\nResponse: {jsonResponse}");
        }
    }

    public void PressA()
    {
        SendButtonCommand(Buttons.ButtonA);
    }

    public void PressB()
    {
        SendButtonCommand(Buttons.ButtonB);
    }

    public void PressLeft()
    {
        SendButtonCommand(Buttons.ButtonLeft);
    }
    public void PressRight()
    {
        SendButtonCommand(Buttons.ButtonRight);
    }
    public void PressUp()
    {
        SendButtonCommand(Buttons.ButtonUp);
    }
    public void PressDown()
    {
        SendButtonCommand(Buttons.ButtonDown);
    }
    public void PressStart()
    {
        SendButtonCommand(Buttons.ButtonStart);
    }
    public void PressSelect()
    {
        SendButtonCommand(Buttons.ButtonSelect);
    }

    public void Dispose()
    {
        try
        {
            if (_client?.Connected == true)
            {
                SendCommandInternal("disconnect", false);
            }
        }
        catch
        {
            // Игнорируем ошибки при закрытии
        }
        finally
        {
            _stream?.Dispose();
            _client?.Dispose();
        }
    }
}