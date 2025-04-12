namespace PokemonRedRL.Models.ReinforcementLearning;

public class ExperienceReplay
{
    private readonly List<Experience> _buffer;
    private readonly int _capacity;
    private readonly Random _random = new();

    public int Count => _buffer.Count;

    public ExperienceReplay(int capacity)
    {
        _capacity = capacity;
        _buffer = new List<Experience>(capacity);
    }

    public void Push(Experience experience)
    {
        if (_buffer.Count >= _capacity)
            _buffer.RemoveAt(0);
        _buffer.Add(experience);
    }

    public List<Experience> Sample(int batchSize)
    {
        return _buffer.OrderBy(_ => _random.Next()).Take(batchSize).ToList();
    }
}