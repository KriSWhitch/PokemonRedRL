using MessagePack;
using PokemonRedRL.Models.Configuration;
using PokemonRedRL.Models.Experience;
using StackExchange.Redis;

public class RedisExperienceRepository : IExperienceRepository, IDisposable
{
    private const string GLOBAL_DATA_KEY = "exp:global";
    private readonly ConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private readonly RedisConfig _redisConfig;

    public RedisExperienceRepository(RedisConfig config)
    {
        _redisConfig = config;

        var configurationOptions = new ConfigurationOptions
        {
            EndPoints = { $"{config.Host}:{config.Port}" },
            AsyncTimeout = config.TimeoutMs,
            SyncTimeout = config.TimeoutMs / 2,
            AbortOnConnectFail = false,
            ConnectRetry = 3,
            ConnectTimeout = 5000
        };

        _redis = ConnectionMultiplexer.Connect(configurationOptions);
        _db = _redis.GetDatabase();
    }

    public async Task AddAsync(ModelExperience exp)
    {
        await _db.ListRightPushAsync(GLOBAL_DATA_KEY, Serialize(exp));
    }

    public async Task AddWithPriorityAsync(ModelExperience exp, float priority)
    {
        var batch = _db.CreateBatch();
        await batch.ListRightPushAsync(GLOBAL_DATA_KEY, Serialize(exp));
        await batch.SortedSetAddAsync("exp:priorities", exp.Id.ToString(), priority);
        batch.Execute();
    }

    public async Task<List<ModelExperience>> SamplePrioritizedAsync(int count)
    {
        // Выборка с учетом приоритетов
        var ids = await _db.SortedSetRandomMembersWithScoresAsync(
            "exp:priorities",
            count);

        return (await _db.ListRangeAsync(GLOBAL_DATA_KEY))
            .Where(x => ids.Any(id =>
                Deserialize(x).Id.ToString() == id.Element))
            .Select(Deserialize)
            .ToList();
    }

    public async Task<List<ModelExperience>> SampleAsync(int count = -1)
    {
        if (count == -1)
        {
            // Постепенная загрузка больших объемов
            var results = new List<ModelExperience>();
            long length = await _db.ListLengthAsync(GLOBAL_DATA_KEY);

            for (long i = 0; i < length; i += _redisConfig.BatchSize)
            {
                var batch = await _db.ListRangeAsync(
                    GLOBAL_DATA_KEY,
                    i,
                    i + _redisConfig.BatchSize - 1);

                results.AddRange(batch.Select(Deserialize));
            }
            return results;
        }

        // Для небольших выборок
        var items = await _db.ListRangeAsync(GLOBAL_DATA_KEY, 0, -1);
        return items
            .OrderBy(_ => Guid.NewGuid())
            .Take(count)
            .Select(Deserialize)
            .ToList();
    }

    private byte[] Serialize(ModelExperience exp) => MessagePackSerializer.Serialize(exp);
    private ModelExperience Deserialize(RedisValue value) => MessagePackSerializer.Deserialize<ModelExperience>(value);

    public void Dispose() => _redis?.Dispose();
}