using PokemonRedRL.Core.Emulator;
using PokemonRedRL.Core.Interfaces;
using PokemonRedRL.Utils.Enums;

namespace PokemonRedRL.Core.Helpers;

public static class ActionExecutor
{
    public static void ExecuteAction(IEmulatorClient emulator, ActionType action)
    {
        switch (action)
        {
            case ActionType.Up: emulator.SendButtonCommand(Buttons.ButtonUp); break;
            case ActionType.Down: emulator.SendButtonCommand(Buttons.ButtonDown); break;
            case ActionType.Left: emulator.SendButtonCommand(Buttons.ButtonLeft); break;
            case ActionType.Right: emulator.SendButtonCommand(Buttons.ButtonRight); break;
            case ActionType.A: emulator.SendButtonCommand(Buttons.ButtonA); break;
            case ActionType.B: emulator.SendButtonCommand(Buttons.ButtonB); break;
        }
    }

    public static string GetActionName(ActionType action)
    {
        return action switch
        {
            ActionType.Up => "Up",
            ActionType.Down => "Down",
            ActionType.Left => "Left",
            ActionType.Right => "Right",
            ActionType.A => "A",
            ActionType.B => "B",
            _ => "Unknown"
        };
    }
}
