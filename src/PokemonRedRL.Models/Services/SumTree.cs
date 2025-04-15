using PokemonRedRL.Models.Experience;

namespace PokemonRedRL.Models.Services;

public class SumTree
{
    private readonly double[] _tree;
    private readonly int _capacity;
    private int _writeIndex;
    private readonly Random _random = new();

    public SumTree(int capacity)
    {
        _capacity = capacity;
        _tree = new double[2 * capacity];
    }

    public void Add(double priority, ModelExperience data)
    {
        _tree[_capacity + _writeIndex] = priority;
        Update(_writeIndex, priority);
        _writeIndex = (_writeIndex + 1) % _capacity;
    }

    public void Update(int index, double priority)
    {
        index += _capacity;
        _tree[index] = priority;

        while (index > 1)
        {
            index /= 2;
            _tree[index] = _tree[2 * index] + _tree[2 * index + 1];
        }
    }

    public (double, ModelExperience, long) Get(double value)
    {
        int index = 1;
        while (index < _capacity)
        {
            if (value <= _tree[2 * index])
            {
                index = 2 * index;
            }
            else
            {
                value -= _tree[2 * index];
                index = 2 * index + 1;
            }
        }
        return (_tree[index], null, index - _capacity);
    }

    public double Total() => _tree[1];
    public int Size() => _capacity;
}