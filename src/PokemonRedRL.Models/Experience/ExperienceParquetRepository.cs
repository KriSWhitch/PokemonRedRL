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

        _flushTimer = new Timer(_ => FlushBuffer(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public void AddExperience(Experience exp)
    {
        _buffer.Enqueue(exp);
        if (_buffer.Count > 1000)
            Task.Run(FlushBuffer); // Запуск в фоне, чтобы не блокировать основной поток
    }

    private void FlushBuffer()
    {
        if (_buffer.IsEmpty) return;

        _fileSemaphore.Wait();
        try
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

            // Создаем временный файл, затем заменяем основной (атомарная операция)
            string tempPath = Path.GetTempFileName();
            try
            {
                using (var writeStream = File.Create(tempPath))
                {
                    ParquetSerializer.SerializeAsync(existingData, writeStream).Wait();
                }
                File.Replace(tempPath, _filePath, null);
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

    public async Task<List<Experience>> SampleAsync(int count = -1) // -1 означает "все"
    {
        await _fileSemaphore.WaitAsync();
        try
        {
            if (!File.Exists(_filePath))
                return new List<Experience>();

            using var stream = File.OpenRead(_filePath);
            var allData = await ParquetSerializer.DeserializeAsync<Experience>(stream);

            return count == -1
                ? allData.ToList()
                : allData.OrderBy(_ => Guid.NewGuid()).Take(count).ToList();
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
            if (!File.Exists(_filePath)) return;

            string backupPath;
            do
            {
                backupPath = Path.Combine(_backupDir, $"exp_{Guid.NewGuid()}.parquet");
            } while (File.Exists(backupPath));

            File.Copy(_filePath, backupPath);

            // Очистка старых бэкапов
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
                catch { /* Игнорируем ошибки удаления */ }
            }
        }
        finally
        {
            _fileSemaphore.Release();
        }
    }

    public void SaveCheckpoint(string checkpointName)
    {
        // Глобальная блокировка для всех операций с чекпоинтами
        _globalCheckpointLock.Wait();
        try
        {
            RetryIOOperation(() =>
            {
                var checkpointPath = Path.Combine(_checkpointsDir, $"{checkpointName}.parquet");
                var tempCheckpointPath = Path.Combine(_checkpointsDir, $"temp_{Guid.NewGuid()}.parquet");

                try
                {
                    // 1. Сначала сохраняем во временный файл
                    using (var sourceStream = new FileStream(
                        _filePath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite)) // Разрешаем параллельное чтение
                    {
                        using (var destStream = File.Create(tempCheckpointPath))
                        {
                            sourceStream.CopyTo(destStream);
                        }
                    }

                    // 2. Атомарная замена файла
                    if (File.Exists(checkpointPath))
                    {
                        File.Delete(checkpointPath);
                    }
                    File.Move(tempCheckpointPath, checkpointPath);
                }
                finally
                {
                    // 3. Очистка временного файла, если что-то пошло не так
                    if (File.Exists(tempCheckpointPath))
                    {
                        File.Delete(tempCheckpointPath);
                    }
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
        // Глобальная блокировка для всех операций с чекпоинтами
        _globalCheckpointLock.Wait();
        try
        {
            RetryIOOperation(() =>
            {
                var checkpointPath = Path.Combine(_checkpointsDir, $"{checkpointName}.parquet");

                if (!File.Exists(checkpointPath))
                    return;

                // 1. Читаем данные из чекпоинта в память
                List<Experience> data;
                using (var stream = new FileStream(
                    checkpointPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite)) // Разрешаем параллельное чтение
                {
                    data = ParquetSerializer.DeserializeAsync<Experience>(stream).Result.ToList();
                }

                // 2. Записываем в основной файл
                using (var stream = new FileStream(
                    _filePath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None)) // Эксклюзивный доступ
                {
                    ParquetSerializer.SerializeAsync(data, stream).Wait();
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
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
            Thread.Sleep(RETRY_DELAY_MS);
            RetryIOOperation(action, retryCount + 1);
        }
    }

    public void Dispose()
    {
        _flushTimer?.Dispose();
        FlushBuffer();
        GC.SuppressFinalize(this);
    }
}