using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PokemonRedRL.Core.Emulator;
using PokemonRedRL.Core.Helpers;
using PokemonRedRL.Core.Interfaces;
using PokemonRedRL.Core.Services;
using PokemonRedRL.Models.Configuration;
using PokemonRedRL.Models.Services;
using PokemonRedRL.Utils.Helpers;
using PokemonRedRL.Utils.Services;
using StackExchange.Redis;

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

                services.AddSingleton<RedisPrioritizedExperience>(provider =>
                    new RedisPrioritizedExperience("localhost:6379"));

                services.AddSingleton<ConnectionMultiplexer>(provider =>
                    ConnectionMultiplexer.Connect("localhost:6379"));

                services.AddHostedService<RedisMaintenanceService>();


                services.AddScoped<NetworkConfig>(provider =>
                    provider.GetRequiredService<NetworkConfigFactory>().Create()
                );

                services.AddScoped<IExperienceRepository>(provider =>
                    new RedisExperienceRepository(
                        new RedisConfig
                        {
                            BackupDir = "D:/Programming/PokemonRedRL/src/data/models/backups",
                            CheckpointsDir = "D:/Programming/PokemonRedRL/src/data/models/checkpoints"
                        }));

                services.AddScoped<ConnectionManager>();
                services.AddScoped<SocketProtocol>();
                services.AddScoped<GameStateSerializer>();
                services.AddScoped<ExplorationAgent>();

                services.AddScoped<IEmulatorClient, MGBAEmulatorClient>();
                services.AddScoped<IRewardCalculatorService, RewardCalculatorService>();
                services.AddScoped<IStatePreprocessorService, StatePreprocessorService>();

                services.AddSingleton<RedisConfig>(new RedisConfig
                {
                    BackupDir = "D:/Programming/PokemonRedRL/src/data/models/backups",
                    CheckpointsDir = "D:/Programming/PokemonRedRL/src/data/models/checkpoints"
                });
                services.AddSingleton<IExperienceRepository, RedisExperienceRepository>();
            })
            .Build();

        const int NUMBER_OF_AGENTS = 5;

        // Оптимальное количество потоков (можно настроить под вашу систему)
        var maxDegreeOfParallelism = Environment.ProcessorCount * 2;

        // Создаем специальный планировщик для контроля потоков
        var taskScheduler = new ConcurrentExclusiveSchedulerPair(
            TaskScheduler.Default,
            maxConcurrencyLevel: maxDegreeOfParallelism).ConcurrentScheduler;

        var agents = new List<ExplorationAgent>();
        var agentTasks = new List<Task>();

        // Создаем отдельный CancellationTokenSource для управления выполнением
        var cts = new CancellationTokenSource();

        try
        {
            // 1. Инициализация агентов с балансировкой
            for (int i = 0; i < NUMBER_OF_AGENTS; i++)
            {
                using var scope = host.Services.CreateScope();
                var agent = scope.ServiceProvider.GetRequiredService<ExplorationAgent>();
                agents.Add(agent);

                Console.WriteLine($"Agent {i + 1} initialized");

                // 2. Немедленный запуск задачи для агента
                var agentTask = Task.Factory.StartNew(async () =>
                {
                    try
                    {
                        while (!cts.Token.IsCancellationRequested)
                        {
                            Console.WriteLine($"Agent {agent.GetHashCode()} starting new episode");
                            await agent.RunEpisodeAsync().ConfigureAwait(false);

                            // Небольшая пауза между эпизодами
                            await Task.Delay(100, cts.Token);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Корректная обработка отмены
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Agent error: {ex.Message}");
                    }
                }, cts.Token, TaskCreationOptions.LongRunning, taskScheduler).Unwrap();

                agentTasks.Add(agentTask);
            }

            // 3. Ожидание завершения по внешнему условию
            // (например, можно добавить проверку времени или другой критерий)
            await Task.Delay(Timeout.InfiniteTimeSpan, cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("All agents stopped gracefully");
        }
        finally
        {
            // 4. Корректное завершение работы
            cts.Cancel();
            await Task.WhenAll(agentTasks).ConfigureAwait(false);

            foreach (var agent in agents)
            {
                if (agent is IDisposable disposable)
                    disposable.Dispose();
            }
        }
    }
}