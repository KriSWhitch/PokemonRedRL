using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PokemonRedRL.ControlPanel;
using PokemonRedRL.ControlPanel.Models;
using PokemonRedRL.ControlPanel.Services;
using PokemonRedRL.ControlPanel.ViewModels;
using PokemonRedRL.Vision;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var path = Path.GetFullPath(args[0]);
        var offPage = args.Contains("--off-page");
        var seconds = int.Parse(args[1]);
        if (seconds is < 1 or > 180) throw new ArgumentOutOfRangeException(nameof(seconds));
        var service = new RunManifestService();
        var source = service.Load(path);
        var preview = new CountingPreview();
        using var model = new MainViewModel(service, new NoAgents(), new NoPing(), preview,
            new WindowEnumerationService());
        model.AgentCount = source.Agents.Count;
        model.RunRootDirectory = Path.Combine(Path.GetDirectoryName(path)!, "preview-harness");
        model.StartCommand.Execute(null);
        foreach (var slot in source.Agents)
            model.AttachWindow(model.AllTiles[slot.Index], new CandidateWindow(
                new IntPtr(slot.WindowHandle!.Value), "Explicit diagnostic attachment", slot.EmulatorProcessId!.Value, "mGBA"));
        var application = new Application();
        var window = new MainWindow(model) { Title = "S9 preview verification - no learner or Lua traffic" };
        var stop = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
        using var ready = args.Length > 2 && !args[2].StartsWith("--") ? EventWaitHandle.OpenExisting(args[2]) : null;
        var clock = Stopwatch.StartNew();
        using var process = Process.GetCurrentProcess();
        var initialCpu = process.TotalProcessorTime;
        window.Closing += (_, _) =>
        {
            stop.Stop();
            if (offPage)
            {
                for (var index = source.Agents.Count; index < 10; index++)
                    model.AllTiles.Add(new AgentTileViewModel(new AgentSlot { Index = index }));
                model.NextPageCommand.Execute(null);
                if (model.CurrentPage != 1 || model.CurrentPageTiles.Any(tile => tile.WindowHandle != IntPtr.Zero))
                    throw new InvalidOperationException("Attached tiles were not off-page.");
                var backend = new Win32CaptureBackend();
                foreach (var slot in source.Agents)
                {
                    var binding = FrameBinding.Load(path, slot.Index, slot.Host, slot.Port,
                        new FrameRegion(74, 69, 191, 173));
                    using var provider = new WindowFrameProvider(binding, backend);
                    var frame = provider.Capture();
                    var distinct = frame.Frame?.Rgb.ToArray().Distinct().Count() ?? 0;
                    if (!frame.Success || distinct < 2)
                        throw new InvalidOperationException($"Off-page capture failed for slot {slot.Index}: {frame.Error}; unique bytes={distinct}.");
                    Console.WriteLine($"PASS off-page live capture slot={slot.Index} page={model.CurrentPage} uniqueBytes={distinct}");
                }
            }
            var rendered = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight,
                96, 96, PixelFormats.Pbgra32);
            rendered.Render(window);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rendered));
            using (var stream = File.Create(Path.Combine(Path.GetDirectoryName(path)!, $"preview-ui-{seconds}s.png")))
                encoder.Save(stream);
            process.Refresh();
            Console.WriteLine(JsonSerializer.Serialize(new { PreviewCalls = preview.Calls,
                UnavailablePreviews = preview.Unavailable, Seconds = clock.Elapsed.TotalSeconds,
                CpuPercent = (process.TotalProcessorTime - initialCpu).TotalSeconds /
                    clock.Elapsed.TotalSeconds / Environment.ProcessorCount * 100,
                PrivateBytes = process.PrivateMemorySize64, GdiHandles = GetGuiResources(process.Handle, 0),
                AttachedTiles = model.AllTiles.Count(tile => tile.WindowHandle != IntPtr.Zero),
                ImagesPresent = model.CurrentPageTiles.Count(tile => tile.PreviewImage is not null) }));
            model.StopCommand.Execute(null);
        };
        stop.Tick += (_, _) => window.Close();
        window.Loaded += (_, _) => { stop.Start(); ready?.Set(); };
        application.Run(window);
        return preview.Calls > 0 && preview.Unavailable == 0 ? 0 : 2;
    }

    [DllImport("user32.dll")] private static extern uint GetGuiResources(IntPtr process, uint flags);

    private sealed class CountingPreview : IPreviewCaptureService
    {
        private readonly PreviewCaptureService _inner = new();
        public int Calls { get; private set; }
        public int Unavailable { get; private set; }
        public BitmapSource? Capture(IntPtr handle)
        {
            Calls++;
            var image = _inner.Capture(handle);
            if (image is null) Unavailable++;
            return image;
        }
    }

    private sealed class NoAgents : IAgentProcessManager
    {
        public event EventHandler<AgentLogLineEventArgs>? LogLineReceived { add { } remove { } }
        public event EventHandler<AgentExitedEventArgs>? AgentExited { add { } remove { } }
        public bool StartAgent(AgentSlot slot) => true;
        public void StopAgent(int index) { }
        public void StopAll() { }
    }

    private sealed class NoPing : IEmulatorPingProbe
    {
        public Task<bool> IsReadyAsync(int port, TimeSpan timeout) => Task.FromResult(false);
    }
}