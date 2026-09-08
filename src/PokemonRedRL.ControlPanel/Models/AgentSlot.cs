namespace PokemonRedRL.ControlPanel.Models;

/// <summary>
/// One emulator + agent process pair managed by the control panel, as persisted in a run manifest.
/// </summary>
public class AgentSlot
{
    public int Index { get; set; }
    public int Port { get; set; }
    public int? EmulatorProcessId { get; set; }
    public int? AgentProcessId { get; set; }
    public long? WindowHandle { get; set; }
    public string RuntimeDirectory { get; set; } = string.Empty;
    public AgentSlotStatus Status { get; set; } = AgentSlotStatus.Pending;
    public DateTime? LastPingUtc { get; set; }
}
