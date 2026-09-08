namespace PokemonRedRL.ControlPanel.Models;

/// <summary>
/// Persisted description of one control-panel run: the ROM/launch/speed settings shared by
/// the run plus the list of agent slots it manages. Saved as JSON at
/// &lt;RunOutputDirectory&gt;/manifest.json (see docs/tasks/automated-mgba-launch-and-fast-forward.md, Step 5).
/// </summary>
public class RunManifest
{
    public string RunId { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public string RomPath { get; set; } = string.Empty;
    public LaunchMode LaunchMode { get; set; } = LaunchMode.ManualAttach;
    public SpeedProfile SpeedProfile { get; set; } = SpeedProfile.Normal;
    public string RunDirectory { get; set; } = string.Empty;
    public List<AgentSlot> Agents { get; set; } = new();
}
