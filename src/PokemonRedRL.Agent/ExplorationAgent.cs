using PokemonRedRL.Core.Emulator;
using PokemonRedRL.Models.ReinforcementLearning;
using static TorchSharp.torch;
using TorchSharp;
using TorchSharp.Modules;
using PokemonRedRL.Core.Enums;
using PokemonRedRL.Utils.Interfaces;
using PokemonRedRL.Utils.Services;

namespace PokemonRedRL.Agent;

public class ExplorationAgent
{
    private readonly MGBASocketClient _emulator;
    private readonly DQNTrainer _trainer;
    private readonly ExperienceReplay _memory;
    private readonly GameState _currentState = new();

    // services
    private readonly IRewardCalculatorService _rewardCalculatorService;
    private readonly IStatePreprocessorService _statePreprocessorService;

    private const float Gamma = 0.99f;
    private const int BatchSize = 32;
    private const int MemoryCapacity = 10000;
    // Input structure:
    // [HP, MapID, X, Y, Direction, Badges, InTrainerBattle, Money, PartyLevels[0-5]]
    private const int InputSize = 14;
    private const int OutputSize = 6; // 6 действий

    private readonly ExperienceParquetRepository _expRepo;
    private DateTime _lastBackupTime = DateTime.MinValue;
    private int _previousBadges;
    private int _previousMoney;
    private List<int> _previousLevels = new();
    private bool _wasInBattle;

    public ExplorationAgent(MGBASocketClient emulator, 
        IRewardCalculatorService rewardCalculatorService,
        IStatePreprocessorService statePreprocessorService)
    {
        _emulator = emulator;
        _rewardCalculatorService = rewardCalculatorService;
        _statePreprocessorService = statePreprocessorService;

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
            var stateTensor = _statePreprocessorService.StateToTensor(_currentState);
            var epsilon = GetEpsilon();
            var action = _trainer.SelectAction(stateTensor, epsilon);

            ExecuteAction(action);
            var (prevMap, prevX, prevY) = (_currentState.MapId, _currentState.X, _currentState.Y);
            UpdateState();

            var isNewLocation = visitedLocations.Add(_currentState.UniqueLocation);
            var reward = _rewardCalculatorService.CalculateReward(_currentState, isNewLocation, prevMap, prevX, prevY);
            totalReward += reward;

            var nextStateTensor = _statePreprocessorService.StateToTensor(_currentState);

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

    private void ExecuteAction(ActionType action)
    {
        switch (action)
        {
            case ActionType.Up: _emulator.PressUp(); break;
            case ActionType.Down: _emulator.PressDown(); break;
            case ActionType.Left: _emulator.PressLeft(); break;
            case ActionType.Right: _emulator.PressRight(); break;
            case ActionType.A: _emulator.PressA(); break;
            case ActionType.B: _emulator.PressB(); break;
        }
    }

    private void LogProgress(ActionType action, float reward, float totalReward, float epsilon)
    {
        Console.WriteLine($"Map: {_currentState.MapId} | Pos: ({_currentState.X},{_currentState.Y}) | " +
            $"Action: {GetActionName(action)} | Reward: {reward:F2} | Total: {totalReward:F2} | ε: {epsilon:F2}");
    }

    private string GetActionName(ActionType action)
    {
        return action switch
        {
            ActionType.Up => "Up",
            ActionType.Down => "Down",
            ActionType.Left => "Left",
            ActionType.Right => "Right",
            ActionType.A => "A",
            ActionType.B => "B",
            _ => "Unknown"
        };
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