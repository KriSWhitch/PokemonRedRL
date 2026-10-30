using System.Diagnostics;
using PokemonRedRL.Agent;
using Xunit;

namespace PokemonRedRL.Vision.Tests;

public class StartupTests
{
    private static async Task<(int ExitCode, string Output)> Start(params string[] args)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
        };
        info.ArgumentList.Add(typeof(AgentRuntimeConfiguration).Assembly.Location);
        foreach (var argument in args) info.ArgumentList.Add(argument);
        info.Environment["REDIS_CONNECTION"] = "127.0.0.1:1,connectTimeout=100,abortConnect=true";
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await process.WaitForExitAsync(timeout.Token);
            return (process.ExitCode, await output + await errors);
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
        }
    }

    [Fact]
    public async Task EnabledLearningIsRejectedBeforeRedisInitialization()
    {
        var result = await Start("--Vision:Enabled", "true", "--vision-diagnostics", "false");
        Assert.Equal(2, result.ExitCode);
        Assert.Contains("not available until S11", result.Output);
        Assert.DoesNotContain("RedisConnectionException", result.Output);
    }

    [Fact]
    public async Task DiagnosticsReportMissingManifestWithoutRedis()
    {
        var result = await Start("--Vision:Enabled", "true", "--vision-diagnostics", "true");
        Assert.Equal(2, result.ExitCode);
        Assert.Contains(OperatingSystem.IsWindows() ? "Supply --manifest" : "interactive Windows desktop", result.Output);
        Assert.DoesNotContain("RedisConnectionException", result.Output);
    }

    [Fact]
    public async Task UnavailableWindowProducesMetricsWithoutRedis()
    {
        if (!OperatingSystem.IsWindows()) return;
        var manifest = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
        try
        {
            await File.WriteAllTextAsync(manifest, """
                {"RunId":"fixture","Agents":[{"Index":0,"Host":"127.0.0.1","Port":12345,
                "EmulatorProcessId":2147483647,"WindowHandle":1}]}
                """);
            var result = await Start("--Vision:Enabled", "true", "--vision-diagnostics", "true",
                "--manifest", manifest, "--agent-index", "0", "--port", "12345",
                "--viewport", "0,0,1,1", "--duration-seconds", "1");
            Assert.Equal(2, result.ExitCode);
            Assert.Contains("VISION_METRICS", result.Output);
            Assert.Contains("\"Completed\":0", result.Output);
            Assert.DoesNotContain("RedisConnectionException", result.Output);
        }
        finally { File.Delete(manifest); }
    }
}