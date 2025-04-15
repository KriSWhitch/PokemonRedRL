using static TorchSharp.torch;
using TorchSharp;
using static TorchSharp.torch.nn;
using PokemonRedRL.Utils.Enums;
using PokemonRedRL.Models.Experience;
using static TorchSharp.torch.optim;
using PokemonRedRL.Models.Services;

namespace PokemonRedRL.Models.ReinforcementLearning;

public class DQNTrainer
{
    private PokeDQN _model;
    private PokeDQN _targetModel;
    private optim.Optimizer _optimizer;
    private int _trainStepCount;
    private readonly int _outputSize;
    private readonly float _learningRate;
    private readonly float _gamma = 0.99f;
    private readonly RedisPrioritizedExperience _redisExp;
    private readonly torch.Device _device;
    private const int MinibatchSize = 32;

    public int TrainStepCount { get => _trainStepCount; private set => _trainStepCount = value; }

    private lr_scheduler.LRScheduler _scheduler;

    public DQNTrainer(string redisConnection, int inputSize, int outputSize, float learningRate = 0.001f, torch.Device device = null)
    {
        _device = device ?? (torch.cuda.is_available() ? torch.CUDA : torch.CPU);
        Console.WriteLine($"Using device: {_device}");

        _model = new PokeDQN(inputSize, outputSize).to(_device);
        _targetModel = new PokeDQN(inputSize, outputSize).to(_device);
        _redisExp = new RedisPrioritizedExperience(redisConnection);
        _targetModel.load_state_dict(_model.state_dict());
        _optimizer = optim.Adam(_model.parameters(), learningRate);
        _outputSize = outputSize;
        _learningRate = learningRate;
        _scheduler = torch.optim.lr_scheduler.StepLR(_optimizer, 100000, 0.1);
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

    public void TrainStepWithPriorities(List<ModelExperience> batch)
    {
        try
        {
            // Проверка на дубликаты
            var duplicateIds = batch
                .GroupBy(x => x.Id)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateIds.Any())
            {
                Console.WriteLine($"Warning: Found {duplicateIds.Count} duplicate IDs in batch");
            }

            // Вычисление TD-ошибок
            var tdErrors = CalculateTdErrors(batch);

            // Создание словаря с обработкой дубликатов
            var updates = new Dictionary<Guid, float>();
            for (int i = 0; i < batch.Count; i++)
            {
                var id = batch[i].Id;
                var priority = Math.Abs(tdErrors[i]);

                if (updates.ContainsKey(id))
                {
                    Console.WriteLine($"Duplicate ID found: {id}, averaging priorities");
                    updates[id] = (updates[id] + priority) / 2; // Усредняем приоритеты
                }
                else
                {
                    updates[id] = priority;
                }
            }

            _redisExp.UpdatePriorities(updates);
            TrainStep(batch, _gamma);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in TrainStepWithPriorities: {ex}");
            throw;
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
                    device: _device).unsqueeze(1);

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

                var targetQ = rewards + gamma * nextQ * (1 - dones.to(torch.float32));
                var currentQ = _model.Forward(states).gather(1, actions).squeeze();

                var loss = (weights * functional.mse_loss(currentQ, targetQ, reduction: Reduction.None)).mean();

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

    private float[] CalculateTdErrors(List<ModelExperience> batch)
    {
        if (batch == null || batch.Count == 0)
            return Array.Empty<float>();

        var tdErrors = new float[batch.Count];

        Tensor states = null;
        Tensor nextStates = null;
        Tensor actions = null;
        Tensor currentQValues = null;
        Tensor gatheredQ = null;
        Tensor nextQValues = null;
        Tensor nextQValuesMax = null;
        Tensor nextQValuesIndices = null;

        try
        {
            // 1. Подготовка тензоров
            var stateList = batch.Select(e => e.State.clone().to(_device)).ToList();
            var nextStateList = batch.Select(e => e.NextState.clone().to(_device)).ToList();

            states = torch.stack(stateList);
            nextStates = torch.stack(nextStateList);

            actions = torch.tensor(
                batch.Select(e => (long)e.Action).ToArray(),
                dtype: torch.int64,
                device: _device).unsqueeze(1);

            // 2. Вычисления в no_grad
            using (no_grad())
            {
                currentQValues = _model.Forward(states);
                gatheredQ = currentQValues.gather(1, actions);
                var currentQ = gatheredQ.squeeze().data<float>().ToArray();

                nextQValues = _targetModel.Forward(nextStates);
                (nextQValuesMax, nextQValuesIndices) = nextQValues.max(1);
                var nextQ = nextQValuesMax.data<float>().ToArray();

                // 3. Вычисление TD-ошибок
                for (int i = 0; i < batch.Count; i++)
                {
                    tdErrors[i] = batch[i].Reward + _gamma * nextQ[i] * (batch[i].Done ? 0 : 1) - currentQ[i];
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"TD Error calculation failed: {ex}");
            Array.Fill(tdErrors, 1.0f);
        }
        finally
        {
            // 4. Освобождение ресурсов в правильном порядке
            nextQValuesIndices?.Dispose();
            nextQValuesMax?.Dispose();
            nextQValues?.Dispose();
            gatheredQ?.Dispose();
            currentQValues?.Dispose();
            actions?.Dispose();
            nextStates?.Dispose();
            states?.Dispose();
        }

        return tdErrors;
    }
}