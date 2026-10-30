using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using PokemonRedRL.Agent;
using PokemonRedRL.ControlPanel.Models;
using PokemonRedRL.ControlPanel.Services;
using PokemonRedRL.Vision;

if (args[0] == "--monitors")
{
    Native.MonitorCallback callback = (IntPtr monitor, IntPtr context, ref Native.Rect bounds, IntPtr data) =>
    {
        var status = Native.GetScaleFactorForMonitor(monitor, out var scale);
        Console.WriteLine(JsonSerializer.Serialize(new { Monitor = monitor.ToInt64(), bounds.Left,
            bounds.Top, bounds.Right, bounds.Bottom, ScalePercent = scale, Status = status }));
        return true;
    };
    Require(Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero), "monitor enumeration");
    return;
}

if (args[0] == "--bind-many")
{
    var handles = args[1].Split(',').Select(long.Parse).ToArray();
    var manifestService = new RunManifestService();
    var run = manifestService.CreateRun(Path.GetFullPath(args[2]), "", handles.Length, 12345,
        LaunchMode.ManualAttach, SpeedProfile.Normal);
    var captureBackend = new Win32CaptureBackend();
    for (var index = 0; index < handles.Length; index++)
    {
        var observed = captureBackend.Inspect(handles[index]) ?? throw new InvalidOperationException("Window unavailable.");
        run.Agents[index].EmulatorProcessId = observed.ProcessId;
        run.Agents[index].WindowHandle = handles[index];
    }
    manifestService.Save(run);
    var manifestPath = Path.Combine(run.RunDirectory, "manifest.json");
    for (var index = 0; index < handles.Length; index++)
    {
        var observed = captureBackend.Inspect(handles[index])!;
        var bounds = new FrameRegion(0, 0, observed.Width, observed.Height);
        var association = FrameBinding.Load(manifestPath, index, "127.0.0.1", 12345 + index, bounds);
        using var source = new WindowFrameProvider(association, captureBackend);
        var captured = source.Capture();
        Require(captured.Success, $"slot {index} capture");
        var destination = Path.Combine(run.RunDirectory, $"slot-{index}-full.bmp");
        typeof(VisionDiagnostics).GetMethod("WriteBitmap", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [destination, captured.Frame!]);
        Console.WriteLine(JsonSerializer.Serialize(new { Index = index, Manifest = manifestPath,
            Snapshot = destination, State = observed }));
    }
    return;
}

var handle = long.Parse(args[0]);
var root = Path.GetFullPath(args[1]);
var backend = new Win32CaptureBackend();
var state = backend.Inspect(handle) ?? throw new InvalidOperationException("Window unavailable.");
var service = new RunManifestService();
var manifest = service.CreateRun(root, "", 1, 12345, LaunchMode.ManualAttach, SpeedProfile.Normal);
manifest.Agents[0].EmulatorProcessId = state.ProcessId;
manifest.Agents[0].WindowHandle = handle;
service.Save(manifest);
var path = Path.Combine(manifest.RunDirectory, "manifest.json");
var region = new FrameRegion(0, 0, state.Width, state.Height);
var binding = FrameBinding.Load(path, 0, "127.0.0.1", 12345, region);
using var provider = new WindowFrameProvider(binding, backend);
var result = provider.Capture();
if (!result.Success) throw new InvalidOperationException(result.Error);
var snapshot = Path.Combine(manifest.RunDirectory, "full-client.bmp");
typeof(VisionDiagnostics).GetMethod("WriteBitmap", BindingFlags.NonPublic | BindingFlags.Static)!
    .Invoke(null, [snapshot, result.Frame!]);
Console.WriteLine(JsonSerializer.Serialize(new { Manifest = path, Snapshot = snapshot, State = state }));

if (args.Contains("--lifecycle"))
{
    var window = new IntPtr(handle);
    if (!Native.GetWindowRect(window, out var original)) throw new InvalidOperationException("Missing window bounds.");
    try
    {
        Native.ShowWindow(window, 6);
        Require(!provider.Capture().Success, "minimized capture rejected");
        Native.ShowWindow(window, 9);
        Require(provider.Capture().Success, "restored window captures successfully");
        Require(Native.SetWindowPos(window, IntPtr.Zero, 0, 0,
            original.Right - original.Left + 32, original.Bottom - original.Top + 32, 0x16), "resize call");
        Require(!provider.Capture().Success, "changed geometry rejected");
    }
    finally
    {
        Native.ShowWindow(window, 9);
        Native.SetWindowPos(window, IntPtr.Zero, original.Left, original.Top,
            original.Right - original.Left, original.Bottom - original.Top, 0x14);
    }
    Require(provider.Capture().Success, "original geometry restored");
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    try { provider.Capture(cancellation.Token); throw new InvalidOperationException("Cancellation was ignored."); }
    catch (OperationCanceledException) { Console.WriteLine("PASS cancelled request rejected"); }
    using var process = System.Diagnostics.Process.GetCurrentProcess();
    var before = Native.GetGuiResources(process.Handle, 0);
    for (var attempt = 0; attempt < 1000; attempt++)
        Require(backend.CaptureRgb(long.MaxValue, region, state) is null, "invalid PrintWindow rejected", false);
    var after = Native.GetGuiResources(process.Handle, 0);
    Require(before == after, $"1000 native PrintWindow failures release GDI resources ({before} -> {after})");
    var owned = Native.CreateWindowEx(0, "STATIC", "S9 owned lifecycle fixture", 0,
        0, 0, 64, 64, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
    Require(owned != IntPtr.Zero, "owned fixture created");
    try
    {
        var ownedBinding = binding with { ProcessId = Environment.ProcessId, WindowHandle = owned.ToInt64(),
            Region = new FrameRegion(0, 0, 16, 16) };
        using var ownedProvider = new WindowFrameProvider(ownedBinding, backend);
        Require(backend.Inspect(owned.ToInt64()) is not null, "owned fixture inspected");
        Native.DestroyWindow(owned);
        owned = IntPtr.Zero;
        Require(!ownedProvider.Capture().Success, "closed window rejected without touching user's emulator");
    }
    finally { if (owned != IntPtr.Zero) Native.DestroyWindow(owned); }
    Console.WriteLine(JsonSerializer.Serialize(new { LifecyclePassed = true, Restored = backend.Inspect(handle) }));
}

static void Require(bool condition, string message, bool report = true)
{
    if (!condition) throw new InvalidOperationException(message);
    if (report) Console.WriteLine("PASS " + message);
}

static class Native
{
    public delegate bool MonitorCallback(IntPtr monitor, IntPtr context, ref Rect bounds, IntPtr data);
    [DllImport("user32.dll")] public static extern bool EnumDisplayMonitors(IntPtr context, IntPtr clip,
        MonitorCallback callback, IntPtr data);
    [DllImport("shcore.dll")] public static extern int GetScaleFactorForMonitor(IntPtr monitor, out int scale);
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr window, IntPtr after, int left,
        int top, int width, int height, uint flags);
    [DllImport("user32.dll")] public static extern uint GetGuiResources(IntPtr process, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "CreateWindowExW")]
    public static extern IntPtr CreateWindowEx(uint extended, string className, string title, uint style,
        int left, int top, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);
    [DllImport("user32.dll")] public static extern bool DestroyWindow(IntPtr window);
}