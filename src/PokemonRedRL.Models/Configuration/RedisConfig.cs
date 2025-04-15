namespace PokemonRedRL.Models.Configuration;

public class RedisConfig
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 6379;
    public string BackupDir { get; set; } = "data/redis_backups";
    public string CheckpointsDir { get; set; } = "data/checkpoints";
    public int TimeoutMs { get; set; } = 30_000; // Увеличиваем таймаут
    public int BatchSize { get; set; } = 10_000; // Размер пачки для загрузки
    public int MaxStreamLength { get; set; } = 1_000_000;
    public int IndexRebuildIntervalMin { get; set; } = 30;
    public int MaintenanceIntervalMinutes { get; set; } = 30;
}