using Parquet;
using Parquet.Serialization;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;

namespace PokemonRedRL.Models.Experience;

public class ExperienceParquetRepository : IDisposable
{
    private const string DEFAULT_DATA_PATH = "D:\\Programming\\PokemonRedRL\\src\\data\\models";
    private const string BACKUPS_SUBDIR = "backups";
    private const string CHECKPOINTS_SUBDIR = "checkpoints";
    private const int MAX_BACKUPS = 10;
    private const int MAX_RETRIES = 3;
    private const int RETRY_DELAY_MS = 500;

    private readonly string _modelsDir;
    private readonly string _backupDir;
    private readonly string _checkpointsDir;
    private readonly string _filePath;
    private readonly ConcurrentQueue<Experience> _buffer = new();
    private readonly Timer _flushTimer;
    private readonly SemaphoreSlim _fileSemaphore = new(1, 1);
    private static readonly SemaphoreSlim _globalCheckpointLock = new(1, 1);

    public ExperienceParquetRepository(string basePath = null)
    {
        _modelsDir = Path.GetFullPath(basePath ?? DEFAULT_DATA_PATH);
        _backupDir = Path.Combine(_modelsDir, BACKUPS_SUBDIR);
        _checkpointsDir = Path.Combine(_modelsDir, CHECKPOINTS_SUBDIR);
        _filePath = Path.Combine(_modelsDir, "shared_experience.parquet");

        Directory.CreateDirectory(_modelsDir);
        Directory.CreateDirectory(_backupDir);
        Directory.CreateDirectory(_checkpointsDir);

        // Создаем пустой файл, если не существует
        if (!File.Exists(_filePath))
        {
            File.Create(_filePath).Dispose();
        }

        _flushTimer = new Timer(_ => FlushBuffer(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public void AddExperience(Experience exp)
    {
        if (exp == null) return;

        _buffer.Enqueue(exp);
        if (_buffer.Count > 1000)
            Task.Run(FlushBuffer);
    }

    private void FlushBuffer()
    {
        if (_buffer.IsEmpty) return;

        _fileSemaphore.Wait();
        try
        {
            var experiencesToSave = new List<Experience>();
            while (_buffer.TryDequeue(out var exp))
            {
                if (exp != null)
                    experiencesToSave.Add(exp);
            }

            if (!experiencesToSave.Any())
                return;

            List<Experience> existingData = new();
            if (File.Exists(_filePath) && new FileInfo(_filePath).Length > 0)
            {
                using var readStream = File.OpenRead(_filePath);
                existingData = ParquetSerializer.DeserializeAsync<Experience>(readStream).Result.ToList();
            }

            existingData.AddRange(experiencesToSave);

            string tempPath = Path.GetTempFileName();
            try
            {
                using (var writeStream = File.Create(tempPath))
                {
                    ParquetSerializer.SerializeAsync(existingData, writeStream).Wait();
                }

                if (File.Exists(_filePath))
                    File.Delete(_filePath);

                File.Move(tempPath, _filePath);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FlushBuffer error: {ex.Message}");
        }
        finally
        {
            _fileSemaphore.Release();
        }
    }

    public async Task<List<Experience>> SampleAsync(int count = -1)
    {
        await _fileSemaphore.WaitAsync();
        try
        {
            if (!File.Exists(_filePath) || new FileInfo(_filePath).Length == 0)
                return new List<Experience>();

            using var stream = File.OpenRead(_filePath);
            var allData = await ParquetSerializer.DeserializeAsync<Experience>(stream);

            return count == -1
                ? allData?.ToList() ?? new List<Experience>()
                : allData?.OrderBy(_ => Guid.NewGuid()).Take(count).ToList() ?? new List<Experience>();
        }
        finally
        {
            _fileSemaphore.Release();
        }
    }

    public async Task CreateBackupAsync()
    {
        await _fileSemaphore.WaitAsync();
        try
        {
            if (!File.Exists(_filePath) || new FileInfo(_filePath).Length == 0)
                return;

            string backupPath;
            do
            {
                backupPath = Path.Combine(_backupDir, $"exp_{Guid.NewGuid()}.parquet");
            } while (File.Exists(backupPath));

            File.Copy(_filePath, backupPath);

            var oldBackups = new DirectoryInfo(_backupDir)
                .GetFiles()
                .OrderByDescending(f => f.CreationTime)
                .Skip(MAX_BACKUPS)
                .ToList();

            foreach (var old in oldBackups)
            {
                try
                {
                    if (old.Exists) old.Delete();
                }
                catch { /* Ignore */ }
            }
        }
        finally
        {
            _fileSemaphore.Release();
        }
    }

    public void SaveCheckpoint(string checkpointName)
    {
        _globalCheckpointLock.Wait();
        try
        {
            RetryIOOperation(() =>
            {
                if (!File.Exists(_filePath) || new FileInfo(_filePath).Length == 0)
                    return;

                var checkpointPath = Path.Combine(_checkpointsDir, $"{checkpointName}.parquet");
                var tempPath = Path.GetTempFileName();

                try
                {
                    using (var source = File.OpenRead(_filePath))
                    using (var dest = File.Create(tempPath))
                    {
                        source.CopyTo(dest);
                    }

                    if (File.Exists(checkpointPath))
                        File.Delete(checkpointPath);

                    File.Move(tempPath, checkpointPath);
                }
                finally
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
            });
        }
        finally
        {
            _globalCheckpointLock.Release();
        }
    }

    public void LoadCheckpoint(string checkpointName)
    {
        _globalCheckpointLock.Wait();
        try
        {
            RetryIOOperation(() =>
            {
                var checkpointPath = Path.Combine(_checkpointsDir, $"{checkpointName}.parquet");
                if (!File.Exists(checkpointPath))
                    return;

                List<Experience> data;
                using (var stream = File.OpenRead(checkpointPath))
                {
                    data = ParquetSerializer.DeserializeAsync<Experience>(stream).Result.ToList();
                }

                var tempPath = Path.GetTempFileName();
                try
                {
                    using (var stream = File.Create(tempPath))
                    {
                        ParquetSerializer.SerializeAsync(data, stream).Wait();
                    }

                    if (File.Exists(_filePath))
                        File.Delete(_filePath);

                    File.Move(tempPath, _filePath);
                }
                finally
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
            });
        }
        finally
        {
            _globalCheckpointLock.Release();
        }
    }

    private void RetryIOOperation(Action action, int retryCount = 0)
    {
        try
        {
            action();
        }
        catch (IOException) when (retryCount < MAX_RETRIES)
        {
            Thread.Sleep(RETRY_DELAY_MS * (retryCount + 1));
            RetryIOOperation(action, retryCount + 1);
        }
    }

    public void Dispose()
    {
        _flushTimer?.Dispose();
        FlushBuffer();
        _fileSemaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}