using static TorchSharp.torch;
using TorchSharp;
using static TorchSharp.torch.nn;

namespace PokemonRedRL.Models.ReinforcementLearning;

public class DQNTrainer
{
    private PokeDQN _model;
    private PokeDQN _targetModel;
    private optim.Optimizer _optimizer;
    private int _updateTargetCounter;
    private readonly int _outputSize;

    public DQNTrainer(int inputSize, int outputSize, float learningRate = 0.001f)
    {
        _model = new PokeDQN(inputSize, outputSize);
        _targetModel = new PokeDQN(inputSize, outputSize);
        _targetModel.load_state_dict(_model.state_dict());
        _optimizer = optim.Adam(_model.parameters(), learningRate);
        _outputSize = outputSize;
    }

    public int SelectAction(Tensor state, float epsilon)
    {
        // Генерация случайного числа для epsilon-greedy
        if (torch.rand(1).item<float>() < epsilon)
        {
            // Используем torch.tensor с явным указанием типа long
            return (int)torch.tensor(new Random().Next(0, _outputSize),
                    dtype: torch.int64).item<long>();
        }

        // Действие на основе модели
        using (no_grad())
        {
            // Добавляем batch-размерность и преобразуем в float
            var input = state.unsqueeze(0).to(torch.float32);
            var qValues = _model.Forward(input);
            return (int)qValues.argmax().item<long>();
        }
    }

    public void TrainStep(List<Experience> batch, float gamma)
    {
        _model.train();

        // Подготовка данных с правильными типами
        var states = torch.stack(batch.Select(e => e.State).ToArray());
        var actions = torch.tensor(
            batch.Select(e => (long)e.Action).ToArray(),  // Явное приведение к long
            dtype: torch.int64);                         // Явное указание типа
        var rewards = torch.tensor(batch.Select(e => e.Reward).ToArray());
        var nextStates = torch.stack(batch.Select(e => e.NextState).ToArray());
        var dones = torch.tensor(
            batch.Select(e => e.Done ? 1L : 0L).ToArray(),  // Используем long
            dtype: torch.int64);

        // Вычисление Q-значений
        var currentQ = _model.Forward(states)
            .gather(1, actions.unsqueeze(1).to(torch.int64));  // Гарантируем int64

        // Вычисление целевых Q-значений
        var nextQ = _targetModel.Forward(nextStates)
            .max(1).values
            .detach();

        var targetQ = rewards + gamma * nextQ * (1 - dones.to(torch.float32));

        // Вычисление потерь
        var loss = functional.mse_loss(
            currentQ.squeeze(),
            targetQ);

        // Оптимизация
        _optimizer.zero_grad();
        loss.backward();
        _optimizer.step();

        // Периодическое обновление целевой модели
        if (++_updateTargetCounter % 100 == 0)
        {
            _targetModel.load_state_dict(_model.state_dict());
        }
    }
}