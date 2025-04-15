using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace PokemonRedRL.Models.Services;

public class RedisMaintenanceService : BackgroundService
{
    private readonly IDatabase _db;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1);
    private readonly TimeSpan _dataTtl = TimeSpan.FromHours(24);
    private const string TempKeyPattern = "temp:exp:*";

    public RedisMaintenanceService(ConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredDataAsync();
                await Task.Delay(_cleanupInterval, stoppingToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cleanup failed: {ex.Message}");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }

    private async Task CleanupExpiredDataAsync()
    {
        // 1. Находим все временные ключи
        var server = _db.Multiplexer.GetServer(_db.Multiplexer.GetEndPoints().First());
        var keys = server.Keys(pattern: TempKeyPattern).ToArray();

        // 2. Проверяем TTL и удаляем просроченные
        var batch = _db.CreateBatch();
        foreach (var key in keys)
        {
            var ttl = await _db.KeyTimeToLiveAsync(key);
            if (ttl == null || ttl <= TimeSpan.Zero)
            {
                await batch.KeyDeleteAsync(key);

                // Удаляем соответствующий приоритет
                var id = key.ToString().Split(':').Last();
                await batch.SortedSetRemoveAsync("exp:priorities", id);
            }
        }
        batch.Execute();

        Console.WriteLine($"Cleaned up {keys.Length} temporary keys");
    }
}