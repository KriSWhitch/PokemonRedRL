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
            AsyncTimeout = config.TimeoutMs,
            SyncTimeout = config.TimeoutMs / 2,
            AbortOnConnectFail = false,
            ConnectRetry = 3,
            ConnectTimeout = 5000
        };

        _redis = ConnectionMultiplexer.Connect(options);
        _db = _redis.GetDatabase();
    }

    public async Task AddAsync(ModelExperience exp)
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
    }

    public async Task<List<ModelExperience>> SampleAsync(int count = -1)
    {
        var entries = await _db.StreamRangeAsync(
            MAIN_STREAM,
            count: count == -1 ? _config.BatchSize : count,
            messageOrder: Order.Descending
        );

        return entries
            .Select(e => MessagePackSerializer.Deserialize<ModelExperience>(e["data"]))
            .ToList();
    }

    public async Task UpdatePrioritiesAsync(Dictionary<Guid, float> updates)
    {
        var transaction = _db.CreateTransaction();

        try
        {
            foreach (var (id, priority) in updates)
            {
                // Добавляем команду в транзакцию БЕЗ await
                _ = transaction.SortedSetAddAsync("exp:priorities", id.ToString(), priority);
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

    private async Task<StreamEntry?> FindEntryById(Guid id)
    {
        try
        {
            var entries = await _db.StreamRangeAsync(MAIN_STREAM);

            // 3. Корректное преобразование RedisValue -> Guid
            return entries.FirstOrDefault(e =>
                e.Values.Any(v =>
                    v.Name == "id" &&
                    Guid.TryParse((string)v.Value, out var guid) &&
                    guid == id
                )
            );
        }
        catch
        {
            return null;
        }
    }

    private List<ModelExperience> ParseSearchResult(RedisResult result)
    {
        var parsed = new List<ModelExperience>();
        var items = (RedisResult[])result;

        for (int i = 1; i < items.Length; i += 2)
        {
            var fields = new List<NameValueEntry>();
            var entries = (RedisResult[])items[i + 1];

            for (int j = 0; j < entries.Length; j += 2)
            {
                fields.Add(new NameValueEntry(
                    (string)entries[j],
                    (RedisValue)entries[j + 1]
                ));
            }

            var data = fields.First(f => f.Name == "data").Value;
            parsed.Add(MessagePackSerializer.Deserialize<ModelExperience>((byte[])data));
        }

        return parsed;
    }

    private byte[] Serialize(ModelExperience exp) => MessagePackSerializer.Serialize(exp);
    private ModelExperience Deserialize(RedisValue value) => MessagePackSerializer.Deserialize<ModelExperience>(value);

    public void Dispose() => _redis?.Dispose();
}