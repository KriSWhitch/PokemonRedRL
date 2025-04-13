namespace PokemonRedRL.DAL.Models;

// Состояние окружения
public class GameState
{
    // Existing properties
    public int MapId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int HP { get; set; }

    // New properties
    public int Direction { get; set; }
    public int Badges { get; set; }
    public int Money { get; set; }
    public List<int> PartyLevels { get; set; } = new();
    public List<PokemonData> Party { get; set; } = new();
    public bool BattleWon { get; set; }
    public bool InTrainerBattle { get; set; }

    public string UniqueLocation => $"{MapId}_{X}_{Y}";
}