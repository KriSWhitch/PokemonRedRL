using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PokemonRedRL.Core.Emulator;
using static TorchSharp.torch;

namespace PokemonRedRL.Utils.Interfaces;

public interface IStatePreprocessorService
{
    public Tensor StateToTensor(GameState state);
}