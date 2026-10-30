using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PokemonRedRL.Vision;

namespace PokemonRedRL.Agent;

public static class VisionDiagnostics
{
    public static async Task<int> RunAsync(IConfiguration configuration)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Vision diagnostics require an interactive Windows desktop.");
        var path = configuration["manifest"] ?? throw new ArgumentException("Supply --manifest <absolute-path>.");
        var index = configuration.GetValue<int?>("agent-index") ?? throw new ArgumentException("Supply --agent-index.");
        var port = configuration.GetValue<int?>("port") ?? throw new ArgumentException("Supply --port.");
        var host = configuration["host"] ?? "127.0.0.1";
        var seconds = configuration.GetValue<int?>("duration-seconds") ?? 60;
        var rate = configuration.GetValue<int?>("capture-hz") ?? 4;
        if (seconds is < 1 or > 7200 || rate is < 1 or > 240)
            throw new ArgumentException("duration-seconds must be 1..7200 and capture-hz must be 1..240.");
        var fields = (configuration["viewport"] ?? "").Split(',');
        if (fields.Length != 4 || !fields.All(field => int.TryParse(field, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out _)))
            throw new ArgumentException("Supply --viewport x,y,width,height in physical client pixels; automatic cropping is not guessed.");
        var values = fields.Select(field => int.Parse(field, CultureInfo.InvariantCulture)).ToArray();
        var binding = FrameBinding.Load(path, index, host, port,
            new FrameRegion(values[0], values[1], values[2], values[3]));
        var output = configuration["frame-output"];
        if (output is not null && (!Path.IsPathFullyQualified(output) ||
            !string.Equals(Path.GetExtension(output), ".bmp", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("frame-output must be a new absolute .bmp path in temporary/run storage.");

        var services = new ServiceCollection();
        services.AddScoped<IWindowCaptureBackend, Win32CaptureBackend>();
        services.AddScoped<IFrameProvider>(provider => new WindowFrameProvider(binding,
            provider.GetRequiredService<IWindowCaptureBackend>()));
        services.AddScoped<IVisionPreprocessorService, VisionPreprocessorService>();
        using var container = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = container.CreateScope();
        var capture = scope.ServiceProvider.GetRequiredService<IFrameProvider>();
        var preprocessing = scope.ServiceProvider.GetRequiredService<IVisionPreprocessorService>();
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += handler;
        using var process = Process.GetCurrentProcess();
        var initialCpu = process.TotalProcessorTime;
        var clock = Stopwatch.StartNew();
        var interval = 1000d / rate;
        var next = 0d;
        var nextReport = 1000d;
        long requested = 0, completed = 0, failed = 0, ready = 0, missed = 0, overflow = 0;
        var histogram = new long[60001];
        string? lastError = null;

        void Report(bool final)
        {
            process.Refresh();
            Console.WriteLine("VISION_METRICS " + JsonSerializer.Serialize(new
            {
                Final = final, binding.RunId, binding.Index, binding.Port, RateHz = rate,
                ElapsedSeconds = clock.Elapsed.TotalSeconds, Requested = requested, Completed = completed,
                Failed = failed, ReadyStacks = ready, MissedDeadlines = missed,
                P50Ms = Percentile(histogram, requested, 0.50), P95Ms = Percentile(histogram, requested, 0.95),
                LatencyOver60Seconds = overflow,
                CpuPercent = (process.TotalProcessorTime - initialCpu).TotalSeconds /
                    Math.Max(clock.Elapsed.TotalSeconds, 0.001) / Environment.ProcessorCount * 100,
                PrivateBytes = process.PrivateMemorySize64, GdiHandles = GetGuiResources(process.Handle, 0),
                LastError = lastError, Snapshot = output
            }));
        }

        try
        {
            while (clock.Elapsed.TotalSeconds < seconds && !cancellation.IsCancellationRequested)
            {
                var wait = next - clock.Elapsed.TotalMilliseconds;
                if (wait > 0) await Task.Delay(TimeSpan.FromMilliseconds(wait), cancellation.Token);
                if (clock.Elapsed.TotalSeconds >= seconds) break;
                var started = Stopwatch.GetTimestamp();
                var frame = capture.Capture(cancellation.Token);
                using var stack = preprocessing.Process(frame);
                requested++;
                if (frame.Success)
                {
                    completed++;
                    if (stack is not null) ready++;
                    if (output is not null && completed == 1) WriteBitmap(output, frame.Frame!);
                    lastError = null;
                }
                else { failed++; lastError = frame.Error; }
                var latency = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                if (latency > 60000) overflow++;
                histogram[(int)Math.Min(Math.Ceiling(latency), 60000)]++;
                next += interval;
                if (clock.Elapsed.TotalMilliseconds > next)
                {
                    var skipped = (long)Math.Floor((clock.Elapsed.TotalMilliseconds - next) / interval) + 1;
                    missed += skipped;
                    next += skipped * interval;
                }
                if (clock.Elapsed.TotalMilliseconds >= nextReport)
                {
                    Report(false);
                    nextReport = clock.Elapsed.TotalMilliseconds + 1000;
                }
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        finally
        {
            Console.CancelKeyPress -= handler;
            Report(true);
        }
        return ready > 0 && failed == 0 ? 0 : 2;
    }

    private static int? Percentile(long[] histogram, long count, double fraction)
    {
        if (count == 0) return null;
        var target = (long)Math.Ceiling(count * fraction);
        long seen = 0;
        for (var index = 0; index < histogram.Length; index++)
        {
            seen += histogram[index];
            if (seen >= target) return index;
        }
        return null;
    }

    private static void WriteBitmap(string path, Frame frame)
    {
        var stride = checked((frame.Width * 3 + 3) & ~3);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        using var writer = new BinaryWriter(stream);
        writer.Write((ushort)0x4D42);
        writer.Write(checked(54 + stride * frame.Height));
        writer.Write(0);
        writer.Write(54);
        writer.Write(40);
        writer.Write(frame.Width);
        writer.Write(frame.Height);
        writer.Write((ushort)1);
        writer.Write((ushort)24);
        writer.Write(0);
        writer.Write(checked(stride * frame.Height));
        writer.Write(new byte[16]);
        var row = new byte[stride];
        for (var y = frame.Height - 1; y >= 0; y--)
        {
            for (var x = 0; x < frame.Width; x++)
            {
                var source = (y * frame.Width + x) * 3;
                row[x * 3] = frame.Rgb.Span[source + 2];
                row[x * 3 + 1] = frame.Rgb.Span[source + 1];
                row[x * 3 + 2] = frame.Rgb.Span[source];
            }
            writer.Write(row);
        }
    }

    [DllImport("user32.dll")] private static extern uint GetGuiResources(IntPtr process, uint flags);
}