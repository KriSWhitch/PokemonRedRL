using StackExchange.Redis;
using static TorchSharp.torch;
using TorchSharp;
using PokemonRedRL.Models.Services;

namespace PokemonRedRL.Models.ReinforcementLearning;

public class AdaptiveLRScheduler
{
    private readonly float _baseLR;
    private readonly int _windowSize;
    private readonly ParameterServer _paramServer; // Используем ParameterServer для Redis

    private float _currentLR;
    private Queue<float> _rewardHistory = new Queue<float>();

    public float CurrentLR => _currentLR;

    public AdaptiveLRScheduler(float baseLR, int windowSize, ParameterServer paramServer)
    {
        _baseLR = _currentLR = baseLR;
        _windowSize = windowSize;
        _paramServer = paramServer;
    }

    public void Update(float episodeReward)
    {
        _rewardHistory.Enqueue(episodeReward);
        if (_rewardHistory.Count > _windowSize)
            _rewardHistory.Dequeue();

        if (_rewardHistory.Count < _windowSize) return;

        var currentAvg = _rewardHistory.Average();
        var prevAvg = currentAvg - (episodeReward - _rewardHistory.Peek()) / _windowSize;

        // Логика изменения LR с защитой
        if (currentAvg > prevAvg * 1.05)
            _currentLR = Math.Min(_currentLR * 1.1f, _baseLR * 2);
        else if (currentAvg < prevAvg * 0.95)
            _currentLR = Math.Max(_currentLR * 0.9f, _baseLR / 10);

        Console.WriteLine($"LR: {_currentLR:F6}, Avg Reward: {currentAvg:F2}");
    }

    // Загрузка состояния из Redis
    public async Task LoadStateAsync()
    {
        try
        {
            var stateDict = await _paramServer.PullStateDict("AdaptiveLRScheduler");
            if (stateDict == null)
            {
                // Инициализация по умолчанию
                _currentLR = _baseLR;
                _rewardHistory = new Queue<float>();
                Console.WriteLine("LR Scheduler state initialized with default values.");
                return;
            }

            // Проверка наличия ключей
            if (!stateDict.ContainsKey("currentLR") || !stateDict.ContainsKey("rewardHistory"))
            {
                Console.WriteLine("Invalid LR Scheduler state format.");
                return;
            }

            // Загрузка currentLR
            var currentLRTensor = stateDict["currentLR"];
            if (currentLRTensor.numel() != 1)
            {
                Console.WriteLine("Invalid currentLR tensor shape.");
                return;
            }
            _currentLR = currentLRTensor.item<float>();

            // Загрузка rewardHistory
            var rewardHistoryTensor = stateDict["rewardHistory"];
            if (rewardHistoryTensor.dim() != 1)
            {
                Console.WriteLine("Invalid rewardHistory tensor shape.");
                return;
            }
            var rewardHistoryData = rewardHistoryTensor.data<float>().ToArray();
            _rewardHistory = new Queue<float>(rewardHistoryData);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load LR scheduler state: {ex.Message}");
            // Восстановление значений по умолчанию
            _currentLR = _baseLR;
            _rewardHistory.Clear();
        }
    }

    // Сохранение состояния в Redis
    public void SaveState()
    {
        try
        {
            var stateDict = new Dictionary<string, Tensor>
            {
                { "currentLR", torch.tensor(_currentLR) },
                { "rewardHistory", torch.tensor(_rewardHistory.ToArray()) }
            };
            _paramServer.PushStateDict(stateDict, "AdaptiveLRScheduler");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save LR scheduler state: {ex.Message}");
        }
    }
}