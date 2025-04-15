using MessagePack;
using PokemonRedRL.Models.Experience;
using StackExchange.Redis;

namespace PokemonRedRL.Models.Services;

public class RedisPrioritizedExperience : IDisposable
{
    private readonly ConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private const string DataKey = "exp:data";
    private const string PriorityKey = "exp:priorities";
    private const string TempKeyPrefix = "temp:exp:";

    public RedisPrioritizedExperience(string connectionString)
    {
        _redis = ConnectionMultiplexer.Connect(connectionString);
        _db = _redis.GetDatabase();
    }

    public void Push(ModelExperience exp, float priority)
    {
        var batch = _db.CreateBatch();

        // Сохраняем данные и приоритеты атомарно
        batch.StringSetAsync($"{TempKeyPrefix}{exp.Id}", Serialize(exp));
        batch.SortedSetAddAsync(PriorityKey, exp.Id.ToString(), priority);
        batch.KeyExpireAsync($"{TempKeyPrefix}{exp.Id}", TimeSpan.FromHours(1));

        batch.Execute();
    }

    public List<ModelExperience> Sample(int batchSize)
    {
        // 1. Выбираем batchSize элементов с наибольшими приоритетами
        var idsWithScores = _db.SortedSetRangeByRankWithScores(
            PriorityKey,
            0,
            batchSize - 1,
            Order.Descending);

        // 2. Получаем данные для выбранных ID
        var keys = idsWithScores
            .Select(x => (RedisKey)$"{TempKeyPrefix}{x.Element}")
            .ToArray();

        var values = _db.StringGet(keys);

        // 3. Десериализуем и возвращаем
        return values
            .Where(x => x.HasValue)
            .Select(x => Deserialize(x))
            .ToList();
    }

    public void UpdatePriorities(Dictionary<Guid, float> updates)
    {
        var batch = _db.CreateBatch();
        foreach (var update in updates)
        {
            batch.SortedSetAddAsync(
                PriorityKey,
                update.Key.ToString(),
                update.Value);
        }
        batch.Execute();
    }

    private byte[] Serialize(ModelExperience exp) =>
        MessagePackSerializer.Serialize(exp);

    private ModelExperience Deserialize(RedisValue value) =>
        MessagePackSerializer.Deserialize<ModelExperience>(value);

    public void Dispose() => _redis?.Dispose();
}