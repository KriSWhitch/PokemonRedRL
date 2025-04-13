using PokemonRedRL.Core.Interfaces;
using PokemonRedRL.DAL.Models;

namespace PokemonRedRL.Core.Services;

public class RewardCalculatorService: IRewardCalculatorService
{
    private int _previousBadges;
    private int _previousMoney;
    private List<int> _previousLevels = new();

    public float CalculateReward(GameState currentState, bool isNewLocation, int prevMap, int prevX, int prevY)
    {
        float reward = 0f;

        // Награды
        reward += GetNewLocationReward(currentState, isNewLocation, prevMap, prevX, prevY);
        reward += GetBadgeReward(currentState);
        reward += GetMoneyReward(currentState);
        reward += GetLevelUpReward(currentState);
        reward += GetBattleReward(currentState);

        _previousMoney = currentState.Money;

        return reward / 10f; // Масштабирование для стабильности
    }


    private float GetNewLocationReward(GameState currentState, bool isNewLocation, int prevMap, int prevX, int prevY)
    {
        if (isNewLocation) return 1.0f;
        if (currentState.MapId == prevMap
            && currentState.X == prevX
            && currentState.Y == prevY)
        {
            return -0.01f;
        }

        return 0f;
    }

    private float GetBadgeReward(GameState currentState)
    {
        int newBadges = currentState.Badges - _previousBadges;
        return newBadges > 0 ? newBadges * 100 : 0;
    }

    private float GetMoneyReward(GameState currentState)
    {
        int moneyDiff = currentState.Money - _previousMoney;

        // Начисляем награду только за получение денег (игнорируем потери)
        if (moneyDiff > 0)
        {
            // Начисляем 0.01 очка за каждые 100 денег
            float reward = moneyDiff * 0.0001f;
            _previousMoney = currentState.Money; // Обновляем предыдущее значение
            return reward;
        }

        return 0f; // Возвращаем 0 если денег не прибавилось или уменьшилось
    }

    private float GetBattleReward(GameState currentState)
    {
        if (!currentState.BattleWon) return 0;

        return currentState.InTrainerBattle ? 10 : 1;
    }

    private float GetLevelUpReward(GameState currentState)
    {
        float reward = 0;

        // Только для текущих покемонов в партии
        for (int i = 0; i < currentState.PartyLevels.Count; i++)
        {
            if (i >= _previousLevels.Count)
            {
                // Новый покемон
                if (currentState.PartyLevels[i] > 1)
                    reward += 3 * (currentState.PartyLevels[i] - 1);
                continue;
            }

            int diff = currentState.PartyLevels[i] - _previousLevels[i];
            if (diff > 0) reward += 3 * diff;
        }
        return reward;
    }
}