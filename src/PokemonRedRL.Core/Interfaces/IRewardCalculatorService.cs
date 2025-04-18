﻿using PokemonRedRL.DAL.Models;

namespace PokemonRedRL.Core.Interfaces;

public interface IRewardCalculatorService
{
    public float CalculateReward(GameState currentState, bool isNewLocation);
}
