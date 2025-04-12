using PokemonRedRL.Core.Emulator;
using PokemonRedRL.Models.ReinforcementLearning;
using static TorchSharp.torch;
using TorchSharp;

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
    private const int InputSize = 4; // [hp, map, x, y]
    private const int OutputSize = 8; // 8 действий

    private readonly ExperienceParquetRepository _expRepo;
    private DateTime _lastBackupTime = DateTime.MinValue;

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

            // Резервное копирование каждые 2 часа
            if ((DateTime.Now - _lastBackupTime).TotalHours >= 2)
            {
                await _expRepo.CreateBackupAsync();
                _expRepo.SaveCheckpoint("latest");
                _lastBackupTime = DateTime.Now;
            }

            Thread.Sleep(2000);
        }
    }

    private Tensor StateToTensor(GameState state)
    {
        return torch.tensor(new[]
        {
            state.HP / 100f,    // Нормализованное HP [0-1]
            state.MapId / 100f,  // Нормализованный MapID
            state.X / 20f,       // Нормализованная X координата
            state.Y / 20f        // Нормализованная Y координата
        });
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
            case 6: _emulator.PressSelect(); break;
            case 7: _emulator.PressStart(); break;
        }

        Thread.Sleep(2000);
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
            6 => "Select",
            7 => "Start",
            _ => "Unknown"
        };
    }

    private float CalculateReward(bool isNewLocation, int prevMap, int prevX, int prevY)
    {
        if (isNewLocation) return 1.0f;
        if (_currentState.MapId == prevMap &&
            _currentState.X == prevX &&
            _currentState.Y == prevY) return -0.01f;
        return 0f;
    }

    private float GetEpsilon()
    {
        return 0.1f + (0.9f * MathF.Exp(-0.0001f * _memory.Count));
    }

    private void UpdateState()
    {
        var (hp, mapId, x, y) = _emulator.GetGameState();
        _currentState.HP = hp;
        _currentState.MapId = mapId;
        _currentState.X = x;
        _currentState.Y = y;
    }
}