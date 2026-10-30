using Xunit;

namespace PokemonRedRL.Vision.Tests;

public class CaptureTests
{
    private sealed class Backend : IWindowCaptureBackend
    {
        public WindowState? State { get; set; } = new(101, 1000, 160, 144, 96, false);
        public bool Fail { get; set; }
        public int Calls { get; private set; }
        public Action? DuringCapture { get; set; }
        public WindowState? Inspect(long windowHandle) => State;
        public byte[]? CaptureRgb(long windowHandle, FrameRegion region, WindowState state)
        {
            Calls++;
            DuringCapture?.Invoke();
            return Fail ? null : new byte[region.Width * region.Height * 3];
        }
    }

    private static FrameBinding Binding => new("run", 0, "127.0.0.1", 12345, 101, 111,
        new FrameRegion(0, 0, 160, 144));

    [Fact]
    public void ValidCaptureAndExplicitFailure()
    {
        var backend = new Backend();
        using var provider = new WindowFrameProvider(Binding, backend);
        Assert.True(provider.Capture().Success);
        backend.Fail = true;
        Assert.False(provider.Capture().Success);
        backend.State = null;
        Assert.False(provider.Capture().Success);
    }

    [Fact]
    public void ReplacedMinimizedResizedOrWrongProcessCannotCapture()
    {
        var backend = new Backend();
        var initial = backend.State!;
        using var provider = new WindowFrameProvider(Binding, backend);
        foreach (var invalid in new[]
        {
            initial with { ProcessId = 102 }, initial with { StartedUtcTicks = 2000 },
            initial with { Width = 320 }, initial with { Dpi = 144 }, initial with { Minimized = true }
        })
        {
            backend.State = invalid;
            Assert.False(provider.Capture().Success);
        }
        Assert.Equal(0, backend.Calls);
    }

    [Fact]
    public void BoundsCancellationAndDisposalAreCheckedBeforeAllocation()
    {
        var backend = new Backend();
        using var provider = new WindowFrameProvider(Binding with { Region = new FrameRegion(1, 0, 160, 144) }, backend);
        Assert.False(provider.Capture().Success);
        Assert.Throws<OperationCanceledException>(() => provider.Capture(new CancellationToken(true)));
        provider.Dispose();
        provider.Dispose();
        Assert.Throws<ObjectDisposedException>(() => provider.Capture());
        Assert.Equal(0, backend.Calls);
    }

    [Fact]
    public void IdentityChangesDuringCaptureDiscardPixels()
    {
        var backend = new Backend();
        using var provider = new WindowFrameProvider(Binding, backend);
        backend.DuringCapture = () => backend.State = backend.State! with { StartedUtcTicks = 2000 };
        var result = provider.Capture();
        Assert.False(result.Success);
        Assert.Null(result.Frame);
    }
}