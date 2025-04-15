using PokemonRedRL.Core.Helpers;
using PokemonRedRL.Core.Interfaces;
using PokemonRedRL.DAL.Models;
using PokemonRedRL.Models.Experience;
using PokemonRedRL.Models.ReinforcementLearning;
using PokemonRedRL.Models.Services;
using PokemonRedRL.Utils.Enums;

namespace PokemonRedRL.Agent;

public class ExplorationAgent
{
    private readonly IEmulatorClient _emulator;
    private readonly GameState _currentState = new();

    // services
    private readonly IRewardCalculatorService _rewardCalculatorService;
    private readonly IStatePreprocessorService _statePreprocessorService;
    private readonly IExperienceRepository _expRepo;

    private const float Gamma = 0.99f;
    private const int BatchSize = 32;
    private readonly MemoryCache _sessionCache = new(10_000); // Увеличенный кэш
    private List<ModelExperience> _globalExperience = new();

    private readonly RedisPrioritizedExperience _memory;
    private readonly DQNTrainer _trainer;
    private readonly float _epsilonStart = 1.0f;
    private readonly float _epsilonEnd = 0.01f;
    private readonly float _epsilonDecay = 0.9995f;

    // Input structure:
    // [HP, MapID, X, Y, Direction, Badges, InTrainerBattle, Money, PartyLevels[0-5]]
    private const int InputSize = 14;
    private const int OutputSize = 6; // 6 действий

    public ExplorationAgent(IEmulatorClient emulator, 
        IRewardCalculatorService rewardCalculatorService,
        IStatePreprocessorService statePreprocessorService,
        IExperienceRepository expRepo)
    {
        _emulator = emulator;
        _rewardCalculatorService = rewardCalculatorService;
        _statePreprocessorService = statePreprocessorService;
        _expRepo = expRepo;

        _trainer = new DQNTrainer("localhost:6379", InputSize, OutputSize);
        _memory = new RedisPrioritizedExperience("localhost:6379");
    }

    public async Task RunEpisodeAsync()
    {
        Console.WriteLine($"Starting agent on port {_emulator.CurrentPort}");

        await LoadInitialExperienceAsync();

        UpdateState();
        var totalReward = 0f;
        var visitedLocations = new HashSet<string>();

        while (true)
        {
            var stateTensor = _statePreprocessorService.StateToTensor(_currentState);
            var epsilon = GetEpsilon();
            var action = _trainer.SelectAction(stateTensor, epsilon);

            ActionExecutor.ExecuteAction(_emulator, action);
            var (prevMap, prevX, prevY) = (_currentState.MapId, _currentState.X, _currentState.Y);
            UpdateState();

            var isNewLocation = visitedLocations.Add(_currentState.UniqueLocation);
            var reward = _rewardCalculatorService.CalculateReward(_currentState, isNewLocation, prevMap, prevX, prevY);
            totalReward += reward;

            var nextStateTensor = _statePreprocessorService.StateToTensor(_currentState);

            LogProgress(action, reward, totalReward, epsilon);

            // Комбинированная выборка:
            var batch = _sessionCache.Sample(BatchSize / 2)
                .Concat(_globalExperience
                    .OrderBy(_ => Guid.NewGuid())
                    .Take(BatchSize / 2))
                .ToList();

            if (batch.Any())
            {
                _trainer.TrainStepWithPriorities(batch);
            }

            var experience = new ModelExperience
            {
                State = stateTensor,
                Action = action,
                Reward = reward,
                NextState = nextStateTensor,
                Done = false
            };

            // Сохранение опыта в сессии
            _sessionCache.Add(experience);
            await _expRepo.AddAsync(experience);

            // Периодическое обновление глобальной выборки
            if (_sessionCache.Count % 1000 == 0)
            {
                _globalExperience = await _expRepo.SampleAsync(50_000);
            }
        }
    }

    private async Task LoadInitialExperienceAsync()
    {
        if (!_globalExperience.Any())
        {
            _globalExperience = await _expRepo.SampleAsync(50_000); // -1 = все данные
        }
    }

    private void LogProgress(ActionType action, float reward, float totalReward, float epsilon)
    {
        Console.WriteLine($"Map: {_currentState.MapId} | Pos: ({_currentState.X},{_currentState.Y}) | " +
            $"Action: {ActionExecutor.GetActionName(action)} | Reward: {reward:F2} | Total: {totalReward:F2} | ε: {epsilon:F2}");
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
        _currentState.Party = state.Party;
    }

    private float GetEpsilon()
    {
        return _epsilonEnd + (_epsilonStart - _epsilonEnd) *
               MathF.Exp(-1.0f * _trainer.TrainStepCount * _epsilonDecay);
    }
}