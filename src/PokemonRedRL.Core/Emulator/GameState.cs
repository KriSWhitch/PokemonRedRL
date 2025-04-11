using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PokemonRedRL.Core.Emulator;

// Состояние окружения
public class GameState
{
    public int MapId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int HP { get; set; }

    public string UniqueLocation => $"{MapId}_{X}_{Y}";
}