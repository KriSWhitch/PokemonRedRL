using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Threading;
using PokemonRedRL.ControlPanel.Models;
using PokemonRedRL.ControlPanel.Services;

namespace PokemonRedRL.ControlPanel.ViewModels;

/// <summary>
/// Top-level view model for the control-panel session dashboard + emulator wall (see frozen UX
/// contract in docs/tasks/automated-mgba-launch-and-fast-forward.md). Owns session settings, the
/// paginated tile grid, and the preview/ping timers.
/// </summary>
public class MainViewModel : ViewModelBase, IDisposable
{
    private const int TilesPerPage = 9;
    private static readonly TimeSpan PreviewInterval = TimeSpan.FromMilliseconds(100); // 10 FPS/tile contract
    private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(2);

    private readonly IRunManifestService _manifestService;
    private readonly IAgentProcessManager _agentProcessManager;
    private readonly IEmulatorPingProbe _pingProbe;
    private readonly IPreviewCaptureService _previewCapture;
    private readonly IWindowEnumerationService _windowEnumeration;

    // AgentProcessManager raises its events from Process's background reader threads, not the UI
    // thread — all handlers must marshal through this before touching bound collections/properties.
    private readonly Dispatcher _dispatcher;

    private readonly DispatcherTimer _previewTimer;
    private readonly DispatcherTimer _pingTimer;

    public ObservableCollection<AgentTileViewModel> AllTiles { get; } = new();
    public ObservableCollection<AgentTileViewModel> CurrentPageTiles { get; } = new();

    // ---- Session settings (Frozen UX contract, Step 1) ----
    private string _romPath = @"src\data\roms\pokemon_red.gb";
    public string RomPath { get => _romPath; set => SetField(ref _romPath, value); }

    private int _agentCount = 1;
    public int AgentCount { get => _agentCount; set => SetField(ref _agentCount, Math.Clamp(value, 1, 50)); }

    private int _basePort = 12345;
    public int BasePort { get => _basePort; set => SetField(ref _basePort, value); }

    private string _runRootDirectory = "runs";
    public string RunRootDirectory { get => _runRootDirectory; set => SetField(ref _runRootDirectory, value); }

    private SpeedProfile _speedProfile = SpeedProfile.Normal;
    public SpeedProfile SpeedProfile { get => _speedProfile; set => SetField(ref _speedProfile, value); }

    public Array SpeedProfileOptions => Enum.GetValues(typeof(SpeedProfile));

    // AutomatedLaunch is intentionally not selectable in this pass — see Step 3 finding.
    public LaunchMode LaunchMode => LaunchMode.ManualAttach;

    private bool _isRunning;
    public bool IsRunning { get => _isRunning; set => SetField(ref _isRunning, value); }

    private int _currentPage;
    public int CurrentPage
    {
        get => _currentPage;
        set { if (SetField(ref _currentPage, value)) RefreshCurrentPage(); }
    }

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(AllTiles.Count / (double)TilesPerPage));

    private AgentTileViewModel? _selectedTile;
    public AgentTileViewModel? SelectedTile { get => _selectedTile; set => SetField(ref _selectedTile, value); }

    public RelayCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand NextPageCommand { get; }
    public RelayCommand PreviousPageCommand { get; }

    public MainViewModel(
        IRunManifestService manifestService,
        IAgentProcessManager agentProcessManager,
        IEmulatorPingProbe pingProbe,
        IPreviewCaptureService previewCapture,
        IWindowEnumerationService windowEnumeration)
    {
        _manifestService = manifestService;
        _agentProcessManager = agentProcessManager;
        _pingProbe = pingProbe;
        _previewCapture = previewCapture;
        _windowEnumeration = windowEnumeration;
        _dispatcher = Dispatcher.CurrentDispatcher;

        _agentProcessManager.LogLineReceived += OnLogLineReceived;
        _agentProcessManager.AgentExited += OnAgentExited;

        StartCommand = new RelayCommand(StartRun, _ => !IsRunning);
        StopCommand = new RelayCommand(StopRun, _ => IsRunning);
        NextPageCommand = new RelayCommand(_ => CurrentPage = Math.Min(CurrentPage + 1, TotalPages - 1), _ => CurrentPage < TotalPages - 1);
        PreviousPageCommand = new RelayCommand(_ => CurrentPage = Math.Max(CurrentPage - 1, 0), _ => CurrentPage > 0);

        _previewTimer = new DispatcherTimer { Interval = PreviewInterval };
        _previewTimer.Tick += (_, _) => RefreshPreviews();

        _pingTimer = new DispatcherTimer { Interval = PingInterval };
        _pingTimer.Tick += async (_, _) => await RefreshConnectionStatesAsync();
    }

    private void StartRun(object? _)
    {
        var manifest = _manifestService.CreateRun(
            Path.GetFullPath(RunRootDirectory), RomPath, AgentCount, BasePort, LaunchMode, SpeedProfile);

        AllTiles.Clear();
        foreach (var slot in manifest.Agents)
        {
            AllTiles.Add(new AgentTileViewModel(slot));
        }
        OnPropertyChanged(nameof(TotalPages));
        CurrentPage = 0;

        foreach (var tile in AllTiles)
        {
            _agentProcessManager.StartAgent(tile.Slot);
            tile.Slot.Status = AgentSlotStatus.Starting;
        }
        _manifestService.Save(manifest);

        IsRunning = true;
        _previewTimer.Start();
        _pingTimer.Start();
    }

    private void StopRun(object? _)
    {
        _previewTimer.Stop();
        _pingTimer.Stop();
        _agentProcessManager.StopAll();

        foreach (var tile in AllTiles)
        {
            tile.Slot.Status = AgentSlotStatus.Stopped;
            tile.ConnectionState = AgentConnectionState.Disconnected;
        }
        IsRunning = false;
    }

    /// <summary>Explicit, user-driven window attachment for one tile (see WindowEnumerationService remarks on why this is not automatic).</summary>
    public List<CandidateWindow> GetCandidateWindows() => _windowEnumeration.GetCandidateEmulatorWindows();

    public void AttachWindow(AgentTileViewModel tile, CandidateWindow window)
    {
        tile.WindowHandle = window.Handle;
        tile.Slot.EmulatorProcessId = window.ProcessId;
    }

    private void RefreshCurrentPage()
    {
        CurrentPageTiles.Clear();
        foreach (var tile in AllTiles.Skip(CurrentPage * TilesPerPage).Take(TilesPerPage))
        {
            CurrentPageTiles.Add(tile);
        }
    }

    private void RefreshPreviews()
    {
        foreach (var tile in CurrentPageTiles)
        {
            if (tile.WindowHandle == IntPtr.Zero) continue;
            tile.PreviewImage = _previewCapture.Capture(tile.WindowHandle);
        }
    }

    private async Task RefreshConnectionStatesAsync()
    {
        foreach (var tile in AllTiles)
        {
            var ready = await _pingProbe.IsReadyAsync(tile.Port, TimeSpan.FromMilliseconds(500));
            tile.ConnectionState = ready ? AgentConnectionState.Ready : AgentConnectionState.Disconnected;
            if (ready)
            {
                tile.Slot.LastPingUtc = DateTime.UtcNow;
                if (tile.Slot.Status == AgentSlotStatus.Starting) tile.Slot.Status = AgentSlotStatus.Running;
            }
        }
    }

    private void OnLogLineReceived(object? sender, AgentLogLineEventArgs e)
    {
        // Raised from Process's background reader thread — marshal before touching bound state.
        _dispatcher.BeginInvoke(() =>
        {
            var tile = AllTiles.FirstOrDefault(t => t.Index == e.AgentIndex);
            if (tile is null) return;

            tile.AppendLogLine(e.Line);
            if (e.Telemetry is { } telemetry)
            {
                tile.MapId = telemetry.MapId;
                tile.X = telemetry.X;
                tile.Y = telemetry.Y;
                tile.LastAction = telemetry.Action;
                tile.StepReward = telemetry.Reward;
                tile.TotalReward = telemetry.TotalReward;
                tile.Epsilon = telemetry.Epsilon;
                tile.ConnectionState = AgentConnectionState.Running;
            }
        });
    }

    private void OnAgentExited(object? sender, AgentExitedEventArgs e)
    {
        // Raised from Process's own Exited event, which fires on a thread-pool thread, not the UI thread.
        _dispatcher.BeginInvoke(() =>
        {
            var tile = AllTiles.FirstOrDefault(t => t.Index == e.AgentIndex);
            if (tile is null) return;

            tile.Slot.Status = e.ExitCode == 0 ? AgentSlotStatus.Stopped : AgentSlotStatus.Error;
            tile.ConnectionState = e.ExitCode == 0 ? AgentConnectionState.Disconnected : AgentConnectionState.Error;
            if (e.ExitCode != 0) tile.ErrorMessage = $"Agent process exited with code {e.ExitCode}";
        });
    }

    public void Dispose()
    {
        _previewTimer.Stop();
        _pingTimer.Stop();
        _agentProcessManager.StopAll();
    }
}
