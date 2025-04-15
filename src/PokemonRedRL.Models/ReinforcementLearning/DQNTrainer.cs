using static TorchSharp.torch;
using TorchSharp;
using static TorchSharp.torch.nn;
using PokemonRedRL.Utils.Enums;
using PokemonRedRL.Models.Experience;
using static TorchSharp.torch.optim;
using PokemonRedRL.Models.Services;
using TorchSharp.Modules;

namespace PokemonRedRL.Models.ReinforcementLearning;

public class DQNTrainer
{
    private PokeDQN _model;
    private PokeDQN _targetModel;
    private optim.Optimizer _optimizer;
    private readonly torch.Device _device;
    private int _trainStepCount;
    private readonly int _outputSize;
    private readonly float _gamma = 0.99f;
    private readonly float _learningRate;
    private readonly IExperienceRepository _expRepo;
    private readonly ParameterServer _paramServer;
    private readonly AdaptiveLRScheduler _lrScheduler;

    private const int MinibatchSize = 32;
    private int _syncInterval = 100; // Синхронизация каждые 100 шагов
    private float _smoothedReward = 0f;
    private const float SMOOTHING_FACTOR = 0.9f;

    public int TrainStepCount { get => _trainStepCount; private set => _trainStepCount = value; }

    private lr_scheduler.LRScheduler _scheduler;

    public DQNTrainer(string redisConnection, int inputSize, int outputSize, IExperienceRepository expRepo, ParameterServer paramServer, AdaptiveLRScheduler lrScheduler, float learningRate = 0.001f, torch.Device device = null)
    {
        _device = device ?? (torch.cuda.is_available() ? torch.CUDA : torch.CPU);
        Console.WriteLine($"Using device: {_device}");

        _model = new PokeDQN(inputSize, outputSize).to(_device);
        _targetModel = new PokeDQN(inputSize, outputSize).to(_device);
        _expRepo = expRepo;
        _targetModel.load_state_dict(_model.state_dict());
        _optimizer = optim.Adam(_model.parameters(), learningRate);
        _outputSize = outputSize;
        _learningRate = learningRate;
        _scheduler = torch.optim.lr_scheduler.StepLR(_optimizer, 100000, 0.1);
        _paramServer = paramServer;
        _lrScheduler = lrScheduler;
    }

    public ActionType SelectAction(Tensor state, float epsilon)
    {
        if (torch.rand(1).item<float>() < epsilon)
            return (ActionType)new Random().Next(0, _outputSize);

        using (no_grad())
        {
            var qValues = _model.Forward(state.to(_device));
            return (ActionType)qValues.argmax().item<long>();
        }
    }

    public async Task<float[]> TrainStepWithPriorities(List<ModelExperience> batch)
    {
        using (var scope = torch.NewDisposeScope())
        {
            var uniqueBatch = batch
                .GroupBy(x => x.Id)
                .Select(g => g.First())
                .ToList();

            var tdErrors = CalculateTdErrors(uniqueBatch);

            var updates = uniqueBatch
                .Select((exp, i) => (exp.Id, Priority: Math.Abs(tdErrors[i])))
                .ToDictionary(x => x.Id, x => x.Priority);

            TrainStep(uniqueBatch, _gamma);

            await SyncWithGlobalModel();
            return tdErrors;
        }
    }

    public void TrainStep(List<ModelExperience> batch, float gamma)
    {
        _model.train();

        try
        {
            // 1. Собираем тензоры с явным контролем жизненного цикла
            using (var scope = torch.NewDisposeScope())
            {
                // 2. Создаем тензоры с явным указанием устройства
                var stateList = batch.Select(e =>
                {
                    var t = e.State.to(_device, copy: true);
                    if (t.device != _device) t = t.to(_device, copy: true);
                    return t;
                }).ToList();

                var states = torch.stack(stateList).to(_device);

                var nextStateList = batch.Select(e =>
                {
                    var t = e.NextState.to(_device, copy: true);
                    if (t.device != _device) t = t.to(_device, copy: true);
                    return t;
                }).ToList();

                var nextStates = torch.stack(nextStateList).to(_device);

                // 3. Создаем остальные тензоры
                var actions = torch.tensor(
                    batch.Select(e => (long)e.Action).ToArray(),
                    dtype: torch.int64,
                    device: _device
                ).unsqueeze(-1); // [batch_size, 1]

                var rewards = torch.tensor(
                    batch.Select(e => e.Reward).ToArray(),
                    device: _device);

                var dones = torch.tensor(
                    batch.Select(e => e.Done ? 1L : 0L).ToArray(),
                    dtype: torch.int64,
                    device: _device);

                var weights = torch.tensor(
                    batch.Select(e => e.Weight).ToArray(),
                    device: _device);

                // 4. Вычисления с явным контролем памяти
                Tensor nextQ;
                using (no_grad())
                {
                    var nextActions = _model.Forward(nextStates).argmax(1);
                    nextQ = _targetModel.Forward(nextStates)
                        .gather(1, nextActions.unsqueeze(1))
                        .squeeze();
                }

                var currentQ = _model.Forward(states).gather(1, actions).squeeze(-1); // [batch_size]
                currentQ = currentQ.unsqueeze(-1); // [batch_size, 1]

                var targetQ = rewards + gamma * nextQ * (1 - dones.to(torch.float32));
                targetQ = targetQ.unsqueeze(-1); // Добавляем размерность, если нужно

                var loss = functional.mse_loss(currentQ, targetQ, reduction: Reduction.None).mean();

                _optimizer.zero_grad();
                loss.backward();
                torch.nn.utils.clip_grad_norm_(_model.parameters(), 1.0);
                _optimizer.step();
                _scheduler.step();

                if (++_trainStepCount % 1000 == 0)
                {
                    _targetModel.load_state_dict(_model.state_dict());
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Training step failed: {ex}");
            throw;
        }
    }

    public void TrackEpisodeReward(float episodeReward)
    {
        _smoothedReward = SMOOTHING_FACTOR * _smoothedReward + (1 - SMOOTHING_FACTOR) * episodeReward;
        _lrScheduler.Update(_smoothedReward);
        UpdateOptimizerLR();
        _lrScheduler.SaveState(); // Сохраняем состояние после каждого эпизода
    }

    private void UpdateOptimizerLR()
    {
        foreach (var paramGroup in _optimizer.ParamGroups)
        {
            paramGroup.LearningRate = _lrScheduler.CurrentLR;
        }
    }

    private float[] CalculateTdErrors(List<ModelExperience> batch)
    {
        if (batch == null || batch.Count == 0)
            return Array.Empty<float>();

        using (var scope = torch.NewDisposeScope())
        {
            // 1. Получаем тензоры
            var states = torch.stack(batch.Select(e => e.State.to(_device)));
            var nextStates = torch.stack(batch.Select(e => e.NextState.to(_device)));
            var actions = torch.tensor(batch.Select(e => (long)e.Action).ToArray(), device: _device).unsqueeze(-1);
            var rewards = torch.tensor(batch.Select(e => e.Reward).ToArray(), device: _device);
            var dones = torch.tensor(batch.Select(e => e.Done ? 1L : 0L).ToArray(), dtype: torch.int64, device: _device);

            using (no_grad())
            {
                // 2. Вычисляем currentQ с правильной размерностью
                var currentQ = _model.Forward(states)
                    .gather(1, actions) // [batch_size, 1]
                    .squeeze(-1); // [batch_size]

                // 3. Вычисляем targetQ и приводим к размерности [batch_size]
                var nextQ = _targetModel.Forward(nextStates).max(1).values;
                var targetQ = rewards + _gamma * nextQ * (1 - dones.to(torch.float32));

                // 4. Проверка размерностей
                if (currentQ.dim() != targetQ.dim())
                    currentQ = currentQ.unsqueeze(-1);

                // 5. Вычисляем TD-ошибки
                var tdErrors = (targetQ - currentQ).abs().data<float>().ToArray();
                return tdErrors;
            }
        }
    }

    public async Task SyncWithGlobalModel()
    {
        if (_trainStepCount % _syncInterval != 0) return;

        // Получаем глобальные веса
        var globalStateDict = await _paramServer.PullStateDict();
        if (globalStateDict != null)
        {
            // Загружаем веса в модели
            using (var scope = torch.NewDisposeScope())
            {
                _model.load_state_dict(globalStateDict);
                _targetModel.load_state_dict(globalStateDict);
            }

            // Освобождаем ресурсы
            foreach (var tensor in globalStateDict.Values)
                tensor.Dispose();
        }

        // Сохраняем текущие веса
        var currentStateDict = _model.state_dict();
        await _paramServer.PushStateDict(currentStateDict);

        // Освобождаем ресурсы текущего state_dict
        foreach (var tensor in currentStateDict.Values)
            tensor.Dispose();
    }
}