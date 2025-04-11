using static TorchSharp.torch;

namespace PokemonRedRL.Models.ReinforcementLearning;

public class Experience
{
    public Tensor State { get; set; }
    public int Action { get; set; }
    public float Reward { get; set; }
    public Tensor NextState { get; set; }
    public bool Done { get; set; }
}