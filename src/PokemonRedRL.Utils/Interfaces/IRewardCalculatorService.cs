using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PokemonRedRL.Core.Emulator;

namespace PokemonRedRL.Utils.Interfaces;

public interface IRewardCalculatorService
{
    public float CalculateReward(GameState currentState, bool isNewLocation, int prevMap, int prevX, int prevY);
}
