using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace PokemonRedRL.Models.ReinforcementLearning;

public class PokeDQN : Module
{
    private Sequential net;
    public int inputSize;
    public int outputSize;

    public PokeDQN(int inputSize, int outputSize) : base("DQN")
    {
        this.inputSize = inputSize;
        this.outputSize = outputSize;
        net = Sequential(
            Linear(inputSize, 128),
            ReLU(),
            Linear(128, 64),
            ReLU(),
            Linear(64, outputSize)
        );
        RegisterComponents();
    }

    public Tensor Forward(Tensor x) => net.forward(x);
}