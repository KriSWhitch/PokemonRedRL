namespace PokemonRedRL.ControlPanel.Models;

/// <summary>How the control panel obtains a running mGBA instance for a slot.</summary>
public enum LaunchMode
{
    /// <summary>User manually starts mGBA + loads the bridge script; the control panel only pings/attaches.</summary>
    ManualAttach,

    /// <summary>Control panel starts the mGBA process and injects the bridge script automatically.
    /// Currently unavailable — see Step 3 finding in docs/tasks/automated-mgba-launch-and-fast-forward.md.</summary>
    AutomatedLaunch
}

/// <summary>Emulation/training speed profile applied to a run.</summary>
public enum SpeedProfile
{
    Normal,
    Accelerated
}

/// <summary>Lifecycle status of a single agent slot (emulator + agent process pair).</summary>
public enum AgentSlotStatus
{
    Pending,
    Starting,
    Ready,
    Running,
    Error,
    Stopped
}

/// <summary>Connection state of a tile's socket link to its mGBA bridge, as observed by the control panel.</summary>
public enum AgentConnectionState
{
    Disconnected,
    Connecting,
    Ready,
    Running,
    Error
}
