using PokemonRedRL.Core.Helpers;
using PokemonRedRL.Core.Interfaces;
using PokemonRedRL.DAL.Models;
using PokemonRedRL.Utils.Enums;
using PokemonRedRL.Utils.Helpers;

namespace PokemonRedRL.Core.Emulator;

public class MGBAEmulatorClient : IEmulatorClient
{
    private readonly SocketProtocol _protocol;
    private readonly GameStateSerializer _serializer;
    public int CurrentPort { get; }

    public MGBAEmulatorClient(
        NetworkConfig config,
        ConnectionManager connection,
        SocketProtocol protocol,
        GameStateSerializer serializer)
    {
        _protocol = protocol;
        _serializer = serializer;

        CurrentPort = config.Port;
    }

    public GameStateResponse GetGameState()
    {
        string response = _protocol.SendCommand("get_state");
        return _serializer.Deserialize(response);
    }

    public void SendButtonCommand(Buttons button)
    {
        _protocol.SendCommand(button.ToString());
    }
}