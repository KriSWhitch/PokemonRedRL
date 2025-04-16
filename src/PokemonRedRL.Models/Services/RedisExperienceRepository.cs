using System.Collections.Concurrent;
using MessagePack;
using PokemonRedRL.Models.Configuration;
using PokemonRedRL.Models.Experience;
using StackExchange.Redis;

public class RedisExperienceRepository : IExperienceRepository, IDisposable
{
    private const string MAIN_STREAM = "exp:stream";
    private readonly ConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private readonly RedisConfig _config;

    public RedisExperienceRepository(RedisConfig config)
    {
        _config = config;

        var options = new ConfigurationOptions
        {
            EndPoints = { $"{config.Host}:{config.Port}" },
            AsyncTimeout = 60_000, // 60 секунд
            SyncTimeout = 30_000,
            ConnectTimeout = 10_000,
            AbortOnConnectFail = false,
            ConnectRetry = 5,
            KeepAlive = 180, // Поддержка долгих соединений
            ClientName = "PokemonRedRL"
        };

        _redis = ConnectionMultiplexer.Connect(options);
        _db = _redis.GetDatabase();
    }

    public async Task AddAsync(ModelExperience exp)
    {
        try
        {
            // Сохраняем в Stream с автоматическим ID
            var fields = new NameValueEntry[]
            {
            new("id", exp.Id.ToString()),
            new("data", MessagePackSerializer.Serialize(exp)),
            new("priority", exp.Weight)
            };
            await _db.StreamAddAsync(MAIN_STREAM, fields, messageId: "*");

            // Сохраняем приоритет в Sorted Set
            await _db.SortedSetAddAsync("exp:priorities", exp.Id.ToString(), exp.Weight);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            throw;
        }
    }

    public async Task<List<ModelExperience>> SamplePrioritizedAsync(int count)
    {
        try
        {
            var entries = await _db.SortedSetRangeByScoreAsync(
                "exp:priorities",
                start: 0.5,
                stop: double.PositiveInfinity,
                exclude: Exclude.None,
                order: Order.Descending,
                skip: 0,
                take: count
            );

            // 4. Проверка на null и валидность данных
            if (entries == null) return new List<ModelExperience>();

            return entries
                .Where(entry => entry.HasValue)
                .Select(entry =>
                    MessagePackSerializer.Deserialize<ModelExperience>((byte[])entry)
                )
                .ToList();
        }
        catch (RedisServerException ex) when (ex.Message.Contains("unknown command"))
        {
            return await SampleAsync(count);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            throw;
        }
    }

    public async Task<List<ModelExperience>> SampleAsync(int count = -1)
    {
        try
        {
            const int PAGE_SIZE = 500; // Ограничиваем размер пачки
            var entries = new List<StreamEntry>();
            RedisValue lastId = "0-0"; // Начальный курсор

            while (true)
            {
                var page = await _db.StreamRangeAsync(
                    MAIN_STREAM,
                    minId: lastId,
                    maxId: "+", // Все записи после lastId
                    count: PAGE_SIZE,
                    messageOrder: Order.Descending
                );

                if (page.Length == 0) break;

                entries.AddRange(page);
                lastId = page.Last().Id;

                if (count != -1 && entries.Count >= count) break;
            }

            return entries
                .Take(count == -1 ? _config.BatchSize : count)
                .Select(e => MessagePackSerializer.Deserialize<ModelExperience>(e["data"]))
                .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SampleAsync error: {ex.Message}");
            return new List<ModelExperience>();
        }
    }

    public async Task UpdatePrioritiesAsync(Dictionary<Guid, float> updates)
    {
        var transaction = _db.CreateTransaction();

        try
        {
            foreach (var (id, priority) in updates)
            {
                // Добавляем команду в транзакцию БЕЗ await
                _ = transaction.SortedSetAddAsync(
                    "exp:priorities",
                    id.ToString(),
                    priority,
                    When.Always, // Всегда перезаписывать
                    CommandFlags.FireAndForget
                );
            }

            // Выполняем транзакцию
            bool committed = await transaction.ExecuteAsync();

            if (!committed)
            {
                Console.WriteLine("Транзакция не выполнена. Возможно, конфликт изменений.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка обновления приоритетов: {ex.Message}");
        }
    }

    public void Dispose() => _redis?.Dispose();
}