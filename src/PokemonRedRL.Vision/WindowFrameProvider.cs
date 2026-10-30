namespace PokemonRedRL.Vision;

public sealed record WindowState(int ProcessId, long StartedUtcTicks, int Width, int Height, uint Dpi,
    bool Minimized);

public interface IWindowCaptureBackend
{
    WindowState? Inspect(long windowHandle);
    byte[]? CaptureRgb(long windowHandle, FrameRegion region, WindowState state);
}

public sealed class WindowFrameProvider : IFrameProvider
{
    private readonly FrameBinding _binding;
    private readonly IWindowCaptureBackend _backend;
    private readonly WindowState? _initial;
    private bool _disposed;

    public WindowFrameProvider(FrameBinding binding, IWindowCaptureBackend backend)
    {
        _binding = binding;
        _backend = backend;
        _initial = backend.Inspect(binding.WindowHandle);
    }

    public FrameCapture Capture(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        var state = _backend.Inspect(_binding.WindowHandle);
        if (state is null || state.Minimized || state.ProcessId != _binding.ProcessId || _initial is null ||
            state.StartedUtcTicks != _initial.StartedUtcTicks || state.Width != _initial.Width ||
            state.Height != _initial.Height || state.Dpi != _initial.Dpi)
            return FrameCapture.Unavailable("Window closed, minimized, replaced, resized, or DPI changed; rebind diagnostics.");
        if (!_binding.Region.Fits(state.Width, state.Height))
            return FrameCapture.Unavailable("Game viewport is outside the current client area.");
        var pixels = _backend.CaptureRgb(_binding.WindowHandle, _binding.Region, state);
        cancellationToken.ThrowIfCancellationRequested();
        if (pixels is null || state != _backend.Inspect(_binding.WindowHandle))
            return FrameCapture.Unavailable("Capture failed or window identity/geometry changed during capture.");
        var identity = $"{_binding.RunId}/{_binding.Index}/{_binding.WindowHandle}/{state.StartedUtcTicks}/{_binding.Region}";
        return new FrameCapture(new Frame(_binding.Region.Width, _binding.Region.Height, pixels,
            identity, DateTimeOffset.UtcNow), null);
    }

    public void Dispose() => _disposed = true;
}