using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace PokemonRedRL.Vision.Tests;

public class FrameTests
{
    [Theory]
    [InlineData(0, 0, 160, 144, true)]
    [InlineData(-1, 0, 160, 144, false)]
    [InlineData(0, 0, 0, 144, false)]
    [InlineData(1, 0, 160, 144, false)]
    [InlineData(int.MaxValue, 0, int.MaxValue, 144, false)]
    public void RegionMustFitCapture(int left, int top, int width, int height, bool expected)
    {
        Assert.Equal(expected, new FrameRegion(left, top, width, height).Fits(160, 144));
    }

    [Fact]
    public void FrameCopiesInputPixels()
    {
        byte[] pixels = [255, 0, 0];
        var frame = new Frame(1, 1, pixels, "slot-1", DateTimeOffset.UtcNow);
        pixels[0] = 0;
        Assert.Equal(255, frame.Rgb.Span[0]);
    }

    [Fact]
    public void FrameRejectsInvalidBuffer()
    {
        Assert.Throws<ArgumentException>(() => new Frame(1, 1, new byte[4], "slot-1", DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => new Frame(0, 1, [], "slot-1", DateTimeOffset.UtcNow));
    }
}