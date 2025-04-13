using PokemonRedRL.DAL.Models;
using static TorchSharp.torch;

namespace PokemonRedRL.Core.Interfaces;

public interface IStatePreprocessorService
{
    public Tensor StateToTensor(GameState state);
}