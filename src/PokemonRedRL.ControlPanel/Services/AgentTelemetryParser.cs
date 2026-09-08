using System.Globalization;
using System.Text.RegularExpressions;

namespace PokemonRedRL.ControlPanel.Services;

/// <summary>Parsed contents of one "STATUS ..." telemetry line emitted by ExplorationAgent.LogProgress.</summary>
public record AgentTelemetry(int MapId, int X, int Y, string Action, float Reward, float TotalReward, float Epsilon);

/// <summary>
/// Parses the machine-readable "STATUS key=value ..." line PokemonRedRL.Agent's
/// ExplorationAgent.LogProgress writes alongside its human-readable log line. Only STATUS lines
/// are parsed; any other stdout line is treated as a plain log line for the tile's log panel.
/// </summary>
public static class AgentTelemetryParser
{
    private static readonly Regex StatusRegex = new(
        @"^STATUS map=(?<map>-?\d+) x=(?<x>-?\d+) y=(?<y>-?\d+) action=(?<action>\S+) " +
        @"reward=(?<reward>-?\d+(\.\d+)?) total=(?<total>-?\d+(\.\d+)?) epsilon=(?<epsilon>-?\d+(\.\d+)?)$",
        RegexOptions.Compiled);

    public static bool TryParse(string line, out AgentTelemetry? telemetry)
    {
        telemetry = null;
        var match = StatusRegex.Match(line);
        if (!match.Success) return false;

        var ic = CultureInfo.InvariantCulture;
        telemetry = new AgentTelemetry(
            MapId: int.Parse(match.Groups["map"].Value, ic),
            X: int.Parse(match.Groups["x"].Value, ic),
            Y: int.Parse(match.Groups["y"].Value, ic),
            Action: match.Groups["action"].Value,
            Reward: float.Parse(match.Groups["reward"].Value, ic),
            TotalReward: float.Parse(match.Groups["total"].Value, ic),
            Epsilon: float.Parse(match.Groups["epsilon"].Value, ic));
        return true;
    }
}
