using PokemonRedRL.Core.Emulator;
using PokemonRedRL.Models.ReinforcementLearning;
using static TorchSharp.torch;
using TorchSharp;
using TorchSharp.Modules;

namespace PokemonRedRL.Agent;

public class ExplorationAgent
{
    private readonly MGBASocketClient _emulator;
    private readonly DQNTrainer _trainer;
    private readonly ExperienceReplay _memory;
    private readonly GameState _currentState = new();

    private const float Gamma = 0.99f;
    private const int BatchSize = 32;
    private const int MemoryCapacity = 10000;
    private const int InputSize = 14;
    private const int OutputSize = 6; // 6 действий

    private readonly ExperienceParquetRepository _expRepo;
    private DateTime _lastBackupTime = DateTime.MinValue;
    private int _previousBadges;
    private int _previousMoney;
    private List<int> _previousLevels = new();
    private bool _wasInBattle;

    public ExplorationAgent(MGBASocketClient emulator)
    {
        _emulator = emulator;
        _trainer = new DQNTrainer(InputSize, OutputSize);
        _memory = new ExperienceReplay(MemoryCapacity);
        _expRepo = new ExperienceParquetRepository();
        // Загрузка начального опыта
        _ = LoadInitialExperienceAsync();

        // Загрузка последнего чекпоинта при старте
        _expRepo.LoadCheckpoint("latest");
    }

    private async Task LoadInitialExperienceAsync()
    {
        var samples = await _expRepo.SampleAsync(1000);
        foreach (var s in samples)
            _memory.Push(s);
    }

    public async Task RunEpisodeAsync()
    {
        UpdateState();
        var totalReward = 0f;
        var visitedLocations = new HashSet<string>();

        while (true)
        {
            var stateTensor = StateToTensor(_currentState);
            var epsilon = GetEpsilon();
            var action = _trainer.SelectAction(stateTensor, epsilon);

            ExecuteAction(action);
            var (prevMap, prevX, prevY) = (_currentState.MapId, _currentState.X, _currentState.Y);
            UpdateState();

            var isNewLocation = visitedLocations.Add(_currentState.UniqueLocation);
            var reward = CalculateReward(isNewLocation, prevMap, prevX, prevY);
            totalReward += reward;

            var nextStateTensor = StateToTensor(_currentState);

            if (_memory.Count > BatchSize)
            {
                _trainer.TrainStep(_memory.Sample(BatchSize), Gamma);
            }

            LogProgress(action, reward, totalReward, epsilon);

            // Сохранение опыта
            _expRepo.AddExperience(new Experience
            {
                State = stateTensor,
                Action = action,
                Reward = reward,
                NextState = nextStateTensor,
                Done = false
            });

            // Резервное копирование каждые 6 минут
            if ((DateTime.Now - _lastBackupTime).TotalMinutes >= 6)
            {
                await _expRepo.CreateBackupAsync();
                _expRepo.SaveCheckpoint("latest");
                _lastBackupTime = DateTime.Now;
            }
        }
    }

    private Tensor StateToTensor(GameState state)
    {
        // Нормализация новых признаков
        var features = new List<float>
        {
            state.HP / 100f,         // [0-1]
            state.MapId / 100f,      // [0-1]
            state.X / 20f,           // [0-1]
            state.Y / 20f,           // [0-1]
            state.Direction / 3f,    // [0-1]
            state.Badges / 8f,       // [0-1]
            state.InTrainerBattle ? 1f : 0f,  // [0-1]
            Math.Min(state.Money / 1000000f, 1f)  // [0-1]
        };

        // Уровни покемонов (макс 6)
        foreach (var level in state.PartyLevels)
            features.Add(level / 100f);  // [0-1]

        while (features.Count < 14) // Всего 14 признаков
            features.Add(0);

        return torch.tensor(features.ToArray());
    }

    private void ExecuteAction(int action)
    {
        switch (action)
        {
            case 0: _emulator.PressUp(); break;
            case 1: _emulator.PressDown(); break;
            case 2: _emulator.PressLeft(); break;
            case 3: _emulator.PressRight(); break;
            case 4: _emulator.PressA(); break;
            case 5: _emulator.PressB(); break;
        }
    }

    private void LogProgress(int action, float reward, float totalReward, float epsilon)
    {
        Console.WriteLine($"Map: {_currentState.MapId} | Pos: ({_currentState.X},{_currentState.Y}) | " +
            $"Action: {GetActionName(action)} | Reward: {reward:F2} | Total: {totalReward:F2} | ε: {epsilon:F2}");
    }

    private string GetActionName(int action)
    {
        return action switch
        {
            0 => "Up",
            1 => "Down",
            2 => "Left",
            3 => "Right",
            4 => "A",
            5 => "B",
            _ => "Unknown"
        };
    }

    private float CalculateReward(bool isNewLocation, int prevMap, int prevX, int prevY)
    {
        float reward = 0f;

        // Награды
        reward += GetNewLocationReward(isNewLocation, prevMap, prevX, prevY);
        reward += GetBadgeReward();
        reward += GetMoneyReward();
        reward += GetLevelUpReward();
        reward += GetBattleReward();

        _previousMoney = _currentState.Money;

        return Math.Clamp(reward, -5f, 200f); // Ограничиваем диапазон наград
    }

    private float GetNewLocationReward(bool isNewLocation, int prevMap, int prevX, int prevY)
    {
        if (isNewLocation) return 1.0f;
        if (_currentState.MapId == prevMap 
            && _currentState.X == prevX 
            && _currentState.Y == prevY)
        {
            return -0.01f;
        }

        return 0f;
    }

    private float GetBadgeReward()
    {
        int newBadges = _currentState.Badges - _previousBadges;
        return newBadges > 0 ? newBadges * 100 : 0;
    }

    private float GetMoneyReward()
    {
        int moneyDiff = _currentState.Money - _previousMoney;

        // Начисляем награду только за получение денег (игнорируем потери)
        if (moneyDiff > 0)
        {
            // Начисляем 0.01 очка за каждые 100 денег
            float reward = moneyDiff * 0.0001f;
            _previousMoney = _currentState.Money; // Обновляем предыдущее значение
            return reward;
        }

        return 0f; // Возвращаем 0 если денег не прибавилось или уменьшилось
    }

    private float GetBattleReward()
    {
        if (!_currentState.BattleWon) return 0;

        return _currentState.InTrainerBattle ? 10 : 1;
    }

    private float GetLevelUpReward()
    {
        float reward = 0;

        // Только для текущих покемонов в партии
        for (int i = 0; i < _currentState.PartyLevels.Count; i++)
        {
            if (i >= _previousLevels.Count)
            {
                // Новый покемон
                if (_currentState.PartyLevels[i] > 1)
                    reward += 3 * (_currentState.PartyLevels[i] - 1);
                continue;
            }

            int diff = _currentState.PartyLevels[i] - _previousLevels[i];
            if (diff > 0) reward += 3 * diff;
        }
        return reward;
    }

    private float GetEpsilon()
    {
        return 0.1f + (0.9f * MathF.Exp(-0.0001f * _memory.Count));
    }

    private void UpdateState()
    {
        var state = _emulator.GetGameState();

        // Existing state
        _currentState.HP = state.Hp;
        _currentState.MapId = state.Map;
        _currentState.X = state.X;
        _currentState.Y = state.Y;

        // New state
        _currentState.Direction = state.Direction;
        _currentState.Badges = state.Badges;
        _currentState.Money = state.Money;
        _currentState.PartyLevels = state.Party.Select(p => p.Level).ToList();
        _currentState.BattleWon = state.BattleWon;
        _currentState.InTrainerBattle = state.InTrainerBattle;
    }
}