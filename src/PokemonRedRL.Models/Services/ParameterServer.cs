using StackExchange.Redis;
using TorchSharp;
using static TorchSharp.torch;

namespace PokemonRedRL.Models.Services;

public class ParameterServer
{
    private readonly IDatabase _redis;
    private const string GLOBAL_MODEL_KEY = "model:state_dict";

    public ParameterServer(ConnectionMultiplexer redis)
    {
        _redis = redis.GetDatabase();
    }

    public async Task PushStateDict(Dictionary<string, Tensor> stateDict)
    {
        using (var ms = new MemoryStream())
        using (var writer = new BinaryWriter(ms))
        {
            // Сериализация всех тензоров
            writer.Write(stateDict.Count);
            foreach (var (key, tensor) in stateDict)
            {
                writer.Write(key);
                tensor.Save(writer);
            }
            await _redis.StringSetAsync(GLOBAL_MODEL_KEY, ms.ToArray());
        }
    }

    public async Task<Dictionary<string, Tensor>> PullStateDict()
    {
        var bytes = (byte[]?)await _redis.StringGetAsync(GLOBAL_MODEL_KEY);
        if (bytes == null) return null;

        var stateDict = new Dictionary<string, Tensor>();
        using (var ms = new MemoryStream(bytes))
        using (var reader = new BinaryReader(ms))
        {
            var count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var key = reader.ReadString();
                var tensor = Tensor.Load(reader);
                stateDict[key] = tensor;
            }
        }
        return stateDict;
    }

    public async Task<Dictionary<string, Tensor>> PullStateDict(string key)
    {
        var bytes = (byte[]?)await _redis.StringGetAsync(key);
        if (bytes == null) return null;

        var stateDict = new Dictionary<string, Tensor>();
        using (var ms = new MemoryStream(bytes))
        using (var reader = new BinaryReader(ms))
        {
            var count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var tensorKey = reader.ReadString(); // Читаем ключ из потока
                var tensor = Tensor.Load(reader);
                stateDict[tensorKey] = tensor;
            }
        }
        return stateDict;
    }

    public void PushStateDict(Dictionary<string, Tensor> stateDict, string key)
    {
        using (var ms = new MemoryStream())
        using (var writer = new BinaryWriter(ms))
        {
            writer.Write(stateDict.Count);
            foreach (var (k, v) in stateDict)
            {
                writer.Write(k);
                v.Save(writer);
            }
            _redis.StringSet(key, ms.ToArray());
        }
    }
}