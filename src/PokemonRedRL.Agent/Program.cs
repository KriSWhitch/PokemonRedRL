using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PokemonRedRL.Core.Emulator;
using PokemonRedRL.Core.Helpers;
using PokemonRedRL.Core.Interfaces;
using PokemonRedRL.Core.Services;
using PokemonRedRL.Utils.Helpers;
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
                services.AddSingleton<NetworkConfigFactory>();
                services.AddScoped<NetworkConfig>(provider =>
                    provider.GetRequiredService<NetworkConfigFactory>().Create()
                );

                services.AddScoped<ConnectionManager>();
                services.AddScoped<SocketProtocol>();
                services.AddScoped<GameStateSerializer>();
                services.AddScoped<ExplorationAgent>();

                services.AddScoped<IEmulatorClient, MGBAEmulatorClient>();
                services.AddScoped<IRewardCalculatorService, RewardCalculatorService>();
                services.AddScoped<IStatePreprocessorService, StatePreprocessorService>();
            })
            .Build();

        const int NUMBER_OF_AGENTS = 3;

        // 1. Сначала создаем и подключаем всех агентов
        var agents = new List<ExplorationAgent>();
        for (int i = 0; i < NUMBER_OF_AGENTS; i++)
        {
            using var scope = host.Services.CreateScope();
            var agent = scope.ServiceProvider.GetRequiredService<ExplorationAgent>();
            agents.Add(agent);

            // Инициализация подключения (если нужно)
            Console.WriteLine($"Agent {i + 1} initialized");
        }

        // 2. Затем запускаем все эпизоды параллельно
        var tasks = new List<Task>();
        foreach (var agent in agents)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    Console.WriteLine($"Starting episode");
                    await agent.RunEpisodeAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Agent error: {ex.Message}");
                }
            }));
        }

        await Task.WhenAll(tasks);
    }
}