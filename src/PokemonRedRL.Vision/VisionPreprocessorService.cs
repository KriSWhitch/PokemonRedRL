using TorchSharp;
using static TorchSharp.torch;

namespace PokemonRedRL.Vision;

public interface IVisionPreprocessorService : IDisposable
{
    Tensor? Process(FrameCapture capture);
    void Reset();
}

public sealed class VisionPreprocessorService : IVisionPreprocessorService
{
    private Tensor? _previous;
    private string? _identity;
    private (int Width, int Height) _dimensions;
    private bool _disposed;

    public Tensor? Process(FrameCapture capture)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!capture.Success)
        {
            Reset();
            return null;
        }

        var frame = capture.Frame!;
        if (_identity != frame.SourceIdentity || _dimensions != (frame.Width, frame.Height))
            Reset();

        using var scope = NewDisposeScope();
        var resize = torchvision.transforms.Resize(84, 84);
        var grayscale = torchvision.transforms.Grayscale(1);
        var normalize = torchvision.transforms.Normalize(new double[] { 0.5 }, new double[] { 0.5 });
        try
        {
            var pixels = tensor(frame.Rgb.ToArray(), dtype: ScalarType.Byte)
                .reshape(frame.Height, frame.Width, 3).permute(2, 0, 1).unsqueeze(0).to_type(ScalarType.Float32) / 255f;
            var current = normalize.call(grayscale.call(resize.call(pixels))).squeeze(0);
            Tensor? result = _previous is null ? null : cat(new[] { _previous, current }, 0);
            _previous?.Dispose();
            _previous = current.DetachFromDisposeScope();
            _identity = frame.SourceIdentity;
            _dimensions = (frame.Width, frame.Height);
            return result?.MoveToOuterDisposeScope();
        }
        catch
        {
            Reset();
            throw;
        }
        finally
        {
            (normalize as IDisposable)?.Dispose();
            (grayscale as IDisposable)?.Dispose();
            (resize as IDisposable)?.Dispose();
        }
    }

    public void Reset()
    {
        _previous?.Dispose();
        _previous = null;
        _identity = null;
        _dimensions = default;
    }

    public void Dispose()
    {
        if (_disposed) return;
        Reset();
        _disposed = true;
    }
}