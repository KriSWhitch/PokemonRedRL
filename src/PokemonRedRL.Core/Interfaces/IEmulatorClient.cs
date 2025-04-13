using PokemonRedRL.DAL.Models;
using PokemonRedRL.Utils.Enums;

namespace PokemonRedRL.Core.Interfaces;

public interface IEmulatorClient
{
    public int CurrentPort { get; }
    GameStateResponse GetGameState();
    void SendButtonCommand(Buttons button);
}
