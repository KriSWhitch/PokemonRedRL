namespace PokemonRedRL.DAL.Models;

public class GameStateResponse
{
    // Existing properties
    public int Hp { get; set; }
    public int Map { get; set; }
    public int X { get; set; }
    public int Y { get; set; }

    // New properties
    public int Badges { get; set; }
    public int Money { get; set; }
    public int Direction { get; set; }
    public List<PokemonData> Party { get; set; } = new();
    public bool InBattle { get; set; }
    public bool InTrainerBattle {  get; set; }
    public bool BattleWon { get; set; }
}