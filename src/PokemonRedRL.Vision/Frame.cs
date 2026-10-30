namespace PokemonRedRL.Vision;

public readonly record struct FrameRegion(int X, int Y, int Width, int Height)
{
    public bool Fits(int width, int height) =>
        X >= 0 && Y >= 0 && Width > 0 && Height > 0 &&
        (long)X + Width <= width && (long)Y + Height <= height;
}

public sealed class Frame
{
    public int Width { get; }
    public int Height { get; }
    public ReadOnlyMemory<byte> Rgb { get; }
    public string SourceIdentity { get; }
    public DateTimeOffset CapturedAt { get; }

    public Frame(int width, int height, ReadOnlySpan<byte> rgb, string sourceIdentity,
        DateTimeOffset capturedAt)
    {
        if (width <= 0 || height <= 0 || (long)width * height * 3 != rgb.Length)
            throw new ArgumentException("Expected packed RGB24 pixels with positive dimensions.", nameof(rgb));
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceIdentity);
        Width = width;
        Height = height;
        Rgb = rgb.ToArray();
        SourceIdentity = sourceIdentity;
        CapturedAt = capturedAt;
    }
}

public sealed record FrameCapture(Frame? Frame, string? Error)
{
    public bool Success => Frame is not null && Error is null;
    public static FrameCapture Unavailable(string error) => new(null, error);
}

public interface IFrameProvider : IDisposable
{
    FrameCapture Capture(CancellationToken cancellationToken = default);
}