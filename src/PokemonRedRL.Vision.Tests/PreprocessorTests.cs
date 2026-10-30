using TorchSharp;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace PokemonRedRL.Vision.Tests;

public class PreprocessorTests
{
    private static FrameCapture Solid(byte red, byte green, byte blue, string identity = "slot-1", int width = 1) =>
        new(new Frame(width, 1, Enumerable.Range(0, width).SelectMany(_ => new[] { red, green, blue }).ToArray(),
            identity, DateTimeOffset.UtcNow), null);

    [Fact]
    public void StackRequiresTwoFramesAndPreservesOrder()
    {
        using var service = new VisionPreprocessorService();
        Assert.Null(service.Process(Solid(0, 0, 0)));
        using var result = service.Process(Solid(255, 255, 255));
        Assert.NotNull(result);
        Assert.Equal(new long[] { 2, 84, 84 }, result.shape);
        Assert.Equal(torch.ScalarType.Float32, result.dtype);
        var values = result.data<float>().ToArray();
        Assert.All(values.Take(84 * 84), value => Assert.InRange(value, -1.00001f, -0.99999f));
        Assert.All(values.Skip(84 * 84), value => Assert.InRange(value, 0.9997f, 1.00001f));
    }

    [Fact]
    public void RedUsesRgbChannelOrder()
    {
        using var service = new VisionPreprocessorService();
        Assert.Null(service.Process(Solid(255, 0, 0)));
        using var result = service.Process(Solid(255, 0, 0));
        Assert.All(result!.data<float>().ToArray(), value => Assert.InRange(value, -0.403f, -0.401f));
    }

    [Fact]
    public void FailureResetIdentityAndGeometryChangesRequireFreshWarmup()
    {
        using var service = new VisionPreprocessorService();
        Assert.Null(service.Process(Solid(0, 0, 0)));
        Assert.Null(service.Process(FrameCapture.Unavailable("closed")));
        Assert.Null(service.Process(Solid(255, 255, 255)));
        Assert.Null(service.Process(Solid(0, 0, 0, "slot-2")));
        Assert.Null(service.Process(Solid(0, 0, 0, "slot-2", 2)));
        service.Reset();
        Assert.Null(service.Process(Solid(255, 255, 255, "slot-2", 2)));
    }

    [Fact]
    public void OutputAndScopesOwnIndependentTensors()
    {
        using var first = new VisionPreprocessorService();
        using var second = new VisionPreprocessorService();
        Assert.Null(first.Process(Solid(0, 0, 0)));
        Assert.Null(second.Process(Solid(255, 255, 255)));
        using (var output = first.Process(Solid(0, 0, 0)))
            Assert.NotNull(output);
        using var next = first.Process(Solid(255, 255, 255));
        using var other = second.Process(Solid(255, 255, 255));
        Assert.InRange(next!.data<float>()[0], -1.00001f, -0.99999f);
        Assert.InRange(other!.data<float>()[0], 0.9997f, 1.00001f);
        first.Dispose();
        first.Dispose();
        Assert.Throws<ObjectDisposedException>(() => first.Process(Solid(0, 0, 0)));
    }

    [Fact]
    public void RepeatedProcessingAndFailuresReleaseOwnedTensors()
    {
        using (var warmup = new VisionPreprocessorService())
        {
            Assert.Null(warmup.Process(Solid(0, 0, 0)));
            using var output = warmup.Process(Solid(255, 255, 255));
        }
        var baseline = torch.Tensor.TotalCount;
        for (var iteration = 0; iteration < 50; iteration++)
        {
            using var service = new VisionPreprocessorService();
            Assert.Null(service.Process(Solid(0, 0, 0)));
            using (var output = service.Process(Solid(255, 255, 255)))
                Assert.NotNull(output);
            Assert.Null(service.Process(FrameCapture.Unavailable("fixture failure")));
        }
        Assert.Equal(baseline, torch.Tensor.TotalCount);
    }

    [Fact]
    public void DiScopesDoNotShareHistoryAndDisposeServices()
    {
        var services = new ServiceCollection();
        services.AddScoped<IVisionPreprocessorService, VisionPreprocessorService>();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        IVisionPreprocessorService first;
        using (var firstScope = provider.CreateScope())
        using (var secondScope = provider.CreateScope())
        {
            first = firstScope.ServiceProvider.GetRequiredService<IVisionPreprocessorService>();
            var second = secondScope.ServiceProvider.GetRequiredService<IVisionPreprocessorService>();
            Assert.NotSame(first, second);
            Assert.Null(first.Process(Solid(0, 0, 0)));
            Assert.Null(second.Process(Solid(255, 255, 255)));
            using var output = first.Process(Solid(255, 255, 255));
            Assert.NotNull(output);
        }
        Assert.Throws<ObjectDisposedException>(() => first.Process(Solid(0, 0, 0)));
    }
}