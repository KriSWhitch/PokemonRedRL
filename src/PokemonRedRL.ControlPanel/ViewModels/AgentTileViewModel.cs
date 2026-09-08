using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using PokemonRedRL.ControlPanel.Models;

namespace PokemonRedRL.ControlPanel.ViewModels;

/// <summary>One visible tile in the emulator wall: one agent slot's live state, as per the frozen UX contract.</summary>
public class AgentTileViewModel : ViewModelBase
{
    public AgentSlot Slot { get; }

    public int Index => Slot.Index;
    public int Port => Slot.Port;

    private AgentConnectionState _connectionState = AgentConnectionState.Disconnected;
    public AgentConnectionState ConnectionState
    {
        get => _connectionState;
        set => SetField(ref _connectionState, value);
    }

    private BitmapSource? _previewImage;
    public BitmapSource? PreviewImage
    {
        get => _previewImage;
        set => SetField(ref _previewImage, value);
    }

    private IntPtr _windowHandle = IntPtr.Zero;
    public IntPtr WindowHandle
    {
        get => _windowHandle;
        set { if (SetField(ref _windowHandle, value)) Slot.WindowHandle = value.ToInt64(); }
    }

    private int _mapId;
    public int MapId { get => _mapId; set => SetField(ref _mapId, value); }

    private int _x;
    public int X { get => _x; set => SetField(ref _x, value); }

    private int _y;
    public int Y { get => _y; set => SetField(ref _y, value); }

    private string _lastAction = string.Empty;
    public string LastAction { get => _lastAction; set => SetField(ref _lastAction, value); }

    private float _stepReward;
    public float StepReward { get => _stepReward; set => SetField(ref _stepReward, value); }

    private float _totalReward;
    public float TotalReward { get => _totalReward; set => SetField(ref _totalReward, value); }

    private float _epsilon;
    public float Epsilon { get => _epsilon; set => SetField(ref _epsilon, value); }

    private string? _errorMessage;
    public string? ErrorMessage { get => _errorMessage; set => SetField(ref _errorMessage, value); }

    public ObservableCollection<string> LogLines { get; } = new();

    public AgentTileViewModel(AgentSlot slot)
    {
        Slot = slot;
    }

    public void AppendLogLine(string line)
    {
        LogLines.Add(line);
        while (LogLines.Count > 500) LogLines.RemoveAt(0);
    }
}
