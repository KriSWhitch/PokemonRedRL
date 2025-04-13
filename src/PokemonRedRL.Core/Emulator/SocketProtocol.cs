using System.Text;
using PokemonRedRL.Core.Helpers;

namespace PokemonRedRL.Core.Emulator;

public class SocketProtocol
{
    private readonly ConnectionManager _connection;
    private readonly NetworkConfig _config;

    public SocketProtocol(ConnectionManager connection, NetworkConfig config)
    {
        _connection = connection;
        _config = config;
    }

    public string SendCommand(string command)
    {
        _connection.EnsureConnected();

        byte[] cmdBytes = Encoding.ASCII.GetBytes(command + "\n");
        _connection.Stream.Write(cmdBytes, 0, cmdBytes.Length);

        var buffer = new byte[1024];
        var response = new StringBuilder();

        while (!response.ToString().EndsWith("\n"))
        {
            int bytesRead = _connection.Stream.Read(buffer, 0, buffer.Length);
            response.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));
        }
        return response.ToString().Trim();
    }
}