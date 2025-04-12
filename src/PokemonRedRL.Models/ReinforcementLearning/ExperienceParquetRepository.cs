using Parquet;
using Parquet.Serialization;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;

namespace PokemonRedRL.Models.ReinforcementLearning;

public class ExperienceParquetRepository : IDisposable
{
    private const string DEFAULT_DATA_PATH = "D:\\Programming\\PokemonRedRL\\src\\data\\models";
    private const string BACKUPS_SUBDIR = "backups";
    private const string CHECKPOINTS_SUBDIR = "checkpoints";

    private readonly string _modelsDir;
    private readonly string _backupDir;
    private readonly string _checkpointsDir;
    private readonly string _filePath;
    private readonly ConcurrentQueue<Experience> _buffer = new();
    private readonly Timer _flushTimer;
    private readonly object _fileLock = new();

    public ExperienceParquetRepository(string basePath = null)
    {
        _modelsDir = Path.GetFullPath(basePath ?? DEFAULT_DATA_PATH);
        _backupDir = Path.Combine(_modelsDir, BACKUPS_SUBDIR);
        _checkpointsDir = Path.Combine(_modelsDir, CHECKPOINTS_SUBDIR);
        _filePath = Path.Combine(_modelsDir, "shared_experience.parquet");

        Directory.CreateDirectory(_modelsDir);
        Directory.CreateDirectory(_backupDir);
        Directory.CreateDirectory(_checkpointsDir);

        _flushTimer = new Timer(_ => FlushBuffer(), null,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(5));
    }

    public void AddExperience(Experience exp)
    {
        _buffer.Enqueue(exp);
        if (_buffer.Count > 1000)
            FlushBuffer();
    }

    private void FlushBuffer()
    {
        if (_buffer.IsEmpty) return;

        lock (_fileLock)
        {
            var experiencesToSave = new List<Experience>();
            while (_buffer.TryDequeue(out var exp))
                experiencesToSave.Add(exp);

            List<Experience> existingData = new();
            if (File.Exists(_filePath))
            {
                using var readStream = File.OpenRead(_filePath);
                existingData = ParquetSerializer.DeserializeAsync<Experience>(readStream).Result.ToList();
            }

            existingData.AddRange(experiencesToSave);

            using var writeStream = File.Create(_filePath);
            ParquetSerializer.SerializeAsync(existingData, writeStream).Wait();
        }
    }

    public async Task<List<Experience>> SampleAsync(int count)
    {
        await Task.Run(FlushBuffer);

        return await Task.Run(async () =>
        {
            lock (_fileLock)
            {
                if (!File.Exists(_filePath))
                    return new List<Experience>();

                using var stream = File.OpenRead(_filePath);
                var allData = ParquetSerializer.DeserializeAsync<Experience>(stream).Result;
                return allData
                    .OrderBy(_ => Guid.NewGuid())
                    .Take(count)
                    .ToList();
            }
        });
    }

    public async Task CreateBackupAsync()
    {
        await Task.Run(() =>
        {
            lock (_fileLock)
            {
                if (!File.Exists(_filePath)) return;

                var backupName = $"exp_{DateTime.Now:yyyyMMdd_HHmmss}.parquet";
                File.Copy(_filePath, Path.Combine(_backupDir, backupName));

                // Очистка старых бэкапов (сохраняем последние 10)
                var oldBackups = new DirectoryInfo(_backupDir)
                    .GetFiles()
                    .OrderBy(f => f.CreationTime)
                    .Take(..^10);

                foreach (var old in oldBackups)
                    old.Delete();
            }
        });
    }

    public void SaveCheckpoint(string checkpointName)
    {
        lock (_fileLock)
        {
            var checkpointPath = Path.Combine(_checkpointsDir, $"{checkpointName}.parquet");
            if (File.Exists(_filePath))
                File.Copy(_filePath, checkpointPath, true);
        }
    }

    public void LoadCheckpoint(string checkpointName)
    {
        lock (_fileLock)
        {
            var checkpointPath = Path.Combine(_checkpointsDir, $"{checkpointName}.parquet");
            if (File.Exists(checkpointPath))
                File.Copy(checkpointPath, _filePath, true);
        }
    }

    public void Dispose()
    {
        _flushTimer?.Dispose();
        FlushBuffer();
        GC.SuppressFinalize(this);
    }
}