using PokemonRedRL.Core.Helpers;
using PokemonRedRL.Core.Interfaces;
using PokemonRedRL.DAL.Models;
using PokemonRedRL.Models.Experience;
using PokemonRedRL.Models.ReinforcementLearning;
using PokemonRedRL.Models.Services;
using PokemonRedRL.Utils.Enums;
using static TorchSharp.torch;

namespace PokemonRedRL.Agent;

public class ExplorationAgent
{
    private readonly IEmulatorClient _emulator;
    private readonly GameState _currentState = new();

    // services
    private readonly IRewardCalculatorService _rewardCalculatorService;
    private readonly IStatePreprocessorService _statePreprocessorService;
    private readonly IExperienceRepository _expRepo;


    // Input structure:
    // [HP, MapID, X, Y, Direction, Badges, InTrainerBattle, Money, PartyLevels[0-5]]
    private const int InputSize = 14;
    private const int OutputSize = 6; // 6 действий
    private const int INITIAL_BATCH_SIZE = 50_000;
    private const int BatchSize = 32;

    private readonly DQNTrainer _trainer;
    private readonly float _epsilonStart = 1.0f;
    private readonly float _epsilonEnd = 0.01f;
    private readonly float _epsilonDecay = 0.9995f;

    private List<ModelExperience> _initialBatch = new();

    public ExplorationAgent(IEmulatorClient emulator, 
        IRewardCalculatorService rewardCalculatorService,
        IStatePreprocessorService statePreprocessorService,
        IExperienceRepository expRepo,
        ParameterServer paramServer,
        AdaptiveLRScheduler lrScheduler)
    {
        _emulator = emulator;
        _rewardCalculatorService = rewardCalculatorService;
        _statePreprocessorService = statePreprocessorService;
        _expRepo = expRepo;

        _trainer = new DQNTrainer("localhost:6379", InputSize, OutputSize, expRepo, paramServer, lrScheduler);
    }

    public async Task RunEpisodeAsync(CancellationTokenSource cts)
    {
        Console.WriteLine($"Starting agent on port {_emulator.CurrentPort}");

        try
        {
            await LoadInitialExperienceAsync();

            if (_initialBatch.Any())
            {
                var tdErrors = await _trainer.TrainStepWithPriorities(_initialBatch);
                await _expRepo.UpdatePrioritiesAsync(
                    _initialBatch.Zip(tdErrors, (exp, err) =>
                        new { exp.Id, Priority = Math.Abs(err) })
                    .ToDictionary(x => x.Id, x => x.Priority)
                );
                _initialBatch.Clear();
            }

            UpdateState();
            const int MAX_EPISODE_STEPS = 1000; // Пример: эпизод длится 1000 шагов
            float episodeReward = 0f;
            int stepCount = 0;
            var visitedLocations = new HashSet<string>();

            try
            {
                while (stepCount++ < MAX_EPISODE_STEPS && !cts.Token.IsCancellationRequested)
                {
                    var stateTensor = _statePreprocessorService.StateToTensor(_currentState);
                    var epsilon = GetEpsilon();
                    var action = _trainer.SelectAction(stateTensor, epsilon);

                    ActionExecutor.ExecuteAction(_emulator, action);
                    var (prevMap, prevX, prevY) = (_currentState.MapId, _currentState.X, _currentState.Y);
                    UpdateState();

                    var isNewLocation = visitedLocations.Add(_currentState.UniqueLocation);
                    var reward = _rewardCalculatorService.CalculateReward(_currentState, isNewLocation, prevMap, prevX, prevY);
                    episodeReward += reward;

                    LogProgress(action, reward, episodeReward, epsilon);

                    var experience = CreateExperience(stateTensor, action, reward);

                    // Сохраняем с приоритетом напрямую
                    await _expRepo.AddAsync(experience);

                    await TrainWithAutoBatch();
                }
            }
            finally
            {
                _trainer.TrackEpisodeReward(episodeReward); // Передача награды за эпизод
                Console.WriteLine($"Episode finished. Total reward: {episodeReward}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Critical error in episode: {ex}");
            throw;
        }
    }

    private async Task TrainWithAutoBatch()
    {
        if (_trainer.TrainStepCount % 10 != 0) return;

        var prioritized = await _expRepo.SamplePrioritizedAsync((int)(BatchSize * 0.7));
        var random = await _expRepo.SampleAsync(BatchSize - prioritized.Count);

        var combined = prioritized.Concat(random).ToList();

        if (combined.Any())
        {
            var tdErrors = await _trainer.TrainStepWithPriorities(combined);
            await UpdatePriorities(combined, tdErrors);
        }
    }

    private ModelExperience CreateExperience(Tensor state, ActionType action, float reward)
    {
        return new ModelExperience
        {
            State = state,
            Action = action,
            Reward = reward,
            NextState = _statePreprocessorService.StateToTensor(_currentState),
            Weight = CalculatePriority(reward)
        };
    }

    private async Task UpdatePriorities(List<ModelExperience> batch, float[]? tdErrors)
    {
        var updates = batch
            .Select((exp, i) => new { exp.Id, Priority = Math.Abs(tdErrors[i]) })
            .ToDictionary(x => x.Id, x => x.Priority);

        await _expRepo.UpdatePrioritiesAsync(updates);
    }

    private float CalculatePriority(float reward)
    {
        return Math.Clamp(reward * 2.0f, 0.1f, 1.0f);
    }

    private async Task LoadInitialExperienceAsync()
    {
        if (_initialBatch.Any()) return;

        var prioritized = await _expRepo.SamplePrioritizedAsync((int)(INITIAL_BATCH_SIZE * 0.7));
        var random = await _expRepo.SampleAsync(INITIAL_BATCH_SIZE - prioritized.Count);

        _initialBatch = prioritized
            .Concat(random)
            .OrderBy(_ => Guid.NewGuid())
            .ToList();
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