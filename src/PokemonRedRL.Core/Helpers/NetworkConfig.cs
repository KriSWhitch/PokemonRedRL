namespace PokemonRedRL.Core.Helpers;

// Новый класс для хранения настроек сети
public class NetworkConfig
{
    public string Host { get; init; } = "127.0.0.1";
    public int Port { get; init; } = 12344;
    public int TimeoutMs { get; init; } = 5000;
    public int MaxRetries { get; init; } = 60;
    public int RetryDelayMs { get; init; } = 1000;
}