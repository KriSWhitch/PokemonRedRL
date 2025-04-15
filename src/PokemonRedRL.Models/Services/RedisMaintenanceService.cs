using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PokemonRedRL.Models.Configuration;
using StackExchange.Redis;

namespace PokemonRedRL.Models.Services;

public class RedisMaintenanceService : BackgroundService
{
    private readonly IDatabase _db;
    private readonly TimeSpan _maintenanceInterval;
    private readonly ILogger<RedisMaintenanceService> _logger;
    private readonly RedisConfig _config;

    public RedisMaintenanceService(
        ConnectionMultiplexer redis,
        ILogger<RedisMaintenanceService> logger,
        RedisConfig config)
    {
        _db = redis.GetDatabase();
        _logger = logger;
        _config = config;
        _maintenanceInterval = TimeSpan.FromMinutes(_config.MaintenanceIntervalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Redis maintenance service");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformMaintenanceAsync();
                await Task.Delay(_maintenanceInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis maintenance error");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        _logger.LogInformation("Stopped Redis maintenance service");
    }

    private async Task PerformMaintenanceAsync()
    {
        _logger.LogInformation("Starting Redis index maintenance");

        // 1. Проверка существования индекса
        var indexExists = await CheckIndexExistsAsync();

        // 2. Удаление старого индекса
        if (indexExists)
        {
            await _db.ExecuteAsync("FT.DROPINDEX", "idx:exp", "DD");
            _logger.LogInformation("Dropped existing index");
        }

        // 3. Создание нового индекса
        await CreateNewIndexAsync();

        _logger.LogInformation("Redis maintenance completed");
    }

    private async Task<bool> CheckIndexExistsAsync()
    {
        try
        {
            await _db.ExecuteAsync("FT.INFO", "idx:exp");
            return true;
        }
        catch (RedisServerException ex) when (ex.Message.Contains("Unknown Index name"))
        {
            return false;
        }
    }

    private async Task CreateNewIndexAsync()
    {
        var result = await _db.ExecuteAsync("FT.CREATE", "idx:exp",
            "ON", "STREAM",
            "PREFIX", "1", "exp:stream",
            "SCHEMA",
            "priority", "NUMERIC", "SORTABLE",
            "timestamp", "NUMERIC", "SORTABLE"
        );

        if (!result.IsNull)
            _logger.LogInformation("Created new Redis search index");
    }
}