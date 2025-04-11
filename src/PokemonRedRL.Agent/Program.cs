using PokemonRedRL.Agent;
using PokemonRedRL.Core.Emulator;

var emulator = new MGBASocketClient();
emulator.Connect();

var agent = new ExplorationAgent(emulator);
agent.RunEpisode();