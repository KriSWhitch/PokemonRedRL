using PokemonRedRL.Models.Experience;

namespace PokemonRedRL.Models.Services;

public class ExperienceReplay
{
    private readonly List<ModelExperience> _buffer;
    private readonly int _capacity;
    private readonly Random _random = new();

    public int Count => _buffer.Count;

    public ExperienceReplay(int capacity)
    {
        _capacity = capacity;
        _buffer = new List<ModelExperience>(capacity);
    }

    public void Push(ModelExperience experience)
    {
        if (_buffer.Count >= _capacity)
            _buffer.RemoveAt(0);
        _buffer.Add(experience);
    }

    public List<ModelExperience> Sample(int batchSize)
    {
        var indices = Enumerable.Range(0, _buffer.Count).ToList();
        // Fisher-Yates shuffle
        for (int i = indices.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (indices[j], indices[i]) = (indices[i], indices[j]);
        }
        return indices.Take(batchSize).Select(i => _buffer[i]).ToList();
    }
}