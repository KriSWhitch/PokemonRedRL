using PokemonRedRL.Core.Emulator;

namespace PokemonRedRL.Agent;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var emulator = new MGBASocketClient();
        emulator.Connect();

        var agent = new ExplorationAgent(emulator);
        await agent.RunEpisodeAsync();
    }
}