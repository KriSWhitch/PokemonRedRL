using static TorchSharp.torch;
using TorchSharp;
using PokemonRedRL.Core.Interfaces;
using PokemonRedRL.DAL.Models;

namespace PokemonRedRL.Utils.Services;

// todo: move all constants like 100f, 8f and others
// in separate constants for EVERY normalization
public class StatePreprocessorService: IStatePreprocessorService
{
    const int MAX_MONEY = 999999; // Максимальное значение денег в Pokémon Red

    public Tensor StateToTensor(GameState state)
    {
        // Нормализация новых признаков
        var features = new List<float>
        {
            NormalizeHP(state.HP),
            NormalizeMapId(state.MapId),
            NormalizeX(state.X),
            NormalizeY(state.Y),
            NormalizeDirection(state.Direction),
            NormalizeBadges(state.Badges),
            NormalizeInTrainerBattle(state.InTrainerBattle),
            NormalizeMoney(state.Money)
        };

        // todo: rework counting of pokemon levels
        // and move normalization to separate method
        // Уровни покемонов (макс 6 покемонов, макс 100 уровней у одного покемона)
        foreach (var level in state.PartyLevels)
            features.Add(level / 100f);  // [0-1]

        while (features.Count < 14) // Всего 14 признаков
            features.Add(0);

        return torch.tensor(features.ToArray());
    }

    private float NormalizeHP(int hp)
    {
        return hp / 100f;
    }

    private float NormalizeMapId(int mapId)
    {
        return mapId / 100f;
    }

    private float NormalizeX(int x)
    {
        // todo: reword normalize of X because X = 20 is NOT maximum X in the game
        // we should process all maps, get current map, it's tileSize
        // coordinates, standart offset will be 20
        // and AI should process all of this to pass more values here
        // and then do better normalize
        return x / 20f;
    }

    private float NormalizeY(int y)
    {
        // todo: reword normalize of Y because y = 20 is NOT maximum Y in the game
        // we should process all maps, get current map, it's tileSize
        // coordinates, standart offset will be 20
        // and AI should process all of this to pass more values here
        // and then do better normalize
        return y / 20f; 
    }

    private float NormalizeDirection(int direction)
    {
        return direction / 3f;
    }

    private float NormalizeBadges(int badges)
    {
        return badges / 8f;
    }

    private float NormalizeInTrainerBattle(bool inTrainerBattle)
    {
        return inTrainerBattle ? 1f : 0f;
    }

    private float NormalizeMoney(int money)
    {
        return money / MAX_MONEY;
    }
}