using System.Collections.Concurrent;
using PokemonRedRL.Models.Experience;

namespace PokemonRedRL.Models.Services;

public class MemoryCache
{
	private readonly ConcurrentDictionary<string, ModelExperience> _cache = new();
	private readonly ConcurrentQueue<string> _queue = new();
	private readonly int _capacity;
	private readonly object _lockObj = new();

	public MemoryCache(int capacity = 50_000)
    {
        _capacity = capacity;
	}
	public void Add(ModelExperience exp)
	{
		lock (_lockObj)
		{
			var key = Guid.NewGuid().ToString();

			// Проверяем, превышено ли количество элементов
			if (_queue.Count >= _capacity)
			{
				// Удаляем первый элемент из очереди и из словаря
				if (_queue.TryDequeue(out var oldKey))
				{
					_cache.TryRemove(oldKey, out _);
				}
			}

			// Добавляем новый элемент
			_cache[key] = exp;
			_queue.Enqueue(key);
		}
	}

	public List<ModelExperience> Sample(int count)
	{
		// Без блокировки для производительности
		var items = _cache.Values.ToArray();
		var rnd = new Random();
		return items.OrderBy(_ => rnd.Next()).Take(count).ToList();
	}

	public int Count => _cache.Count;
}