using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PokemonRedRL.Core.Emulator;
using PokemonRedRL.Utils.Interfaces;
using PokemonRedRL.Utils.Services;

namespace PokemonRedRL.Agent;

internal class Program
{
    private static async Task Main(string[] args)
    {
        // Настройка хоста с DI
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices(services =>
            {
                // Регистрируем зависимости
                services.AddSingleton<MGBASocketClient>();
                services.AddScoped<ExplorationAgent>();
                services.AddScoped<IRewardCalculatorService, RewardCalculatorService>();
                services.AddScoped<IStatePreprocessorService, StatePreprocessorService>();
            })
            .Build();

        var emulator = host.Services.GetRequiredService<MGBASocketClient>();
        emulator.Connect();

        var agent = host.Services.GetRequiredService<ExplorationAgent>();
        await agent.RunEpisodeAsync();
    }
}