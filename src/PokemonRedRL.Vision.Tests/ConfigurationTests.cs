using Microsoft.Extensions.Configuration;
using PokemonRedRL.Agent;
using PokemonRedRL.Core.Services;
using PokemonRedRL.DAL.Models;
using Xunit;

namespace PokemonRedRL.Vision.Tests;

public class ConfigurationTests
{
    [Theory]
    [InlineData(null, false, false)]
    [InlineData("false", false, false)]
    [InlineData("true", true, true)]
    public void ModeIsExplicit(string? enabled, bool diagnostic, bool expected)
    {
        using var configuration = (ConfigurationRoot)new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Vision:Enabled"] = enabled,
            ["vision-diagnostics"] = diagnostic.ToString()
        }).Build();
        Assert.Equal(expected, AgentRuntimeConfiguration.IsDiagnostic(configuration));
    }

    [Theory]
    [InlineData("true", "false")]
    [InlineData("false", "true")]
    public void UnsupportedModesFailBeforeServices(string enabled, string diagnostic)
    {
        using var configuration = (ConfigurationRoot)new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Vision:Enabled"] = enabled, ["vision-diagnostics"] = diagnostic
        }).Build();
        Assert.Throws<InvalidOperationException>(() => AgentRuntimeConfiguration.IsDiagnostic(configuration));
    }

    [Fact]
    public void SlotWorkingDirectoryDoesNotLoseDefaultsAndCommandLineWins()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var slot = Path.Combine(root, "slot");
        Directory.CreateDirectory(slot);
        try
        {
            File.WriteAllText(Path.Combine(root, "appsettings.json"), "{\"S9Fixture\":\"base\"}");
            using (var fallback = AgentRuntimeConfiguration.Load([], root, slot))
                Assert.Equal("base", fallback["S9Fixture"]);
            File.WriteAllText(Path.Combine(slot, "appsettings.json"), "{\"S9Fixture\":\"slot\"}");
            using (var overridden = AgentRuntimeConfiguration.Load([], root, slot))
                Assert.Equal("slot", overridden["S9Fixture"]);
            using var command = AgentRuntimeConfiguration.Load(["--S9Fixture", "command"], root, slot);
            Assert.Equal("command", command["S9Fixture"]);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void MemoryStateMatchesPreVisionFloatBits()
    {
        var state = new GameState
        {
            HP = 37, MapId = 12, X = 7, Y = 19, Direction = 2, Badges = 3,
            InTrainerBattle = true, Money = 500000, PartyLevels = [5, 12, 31, 44, 99, 100]
        };
        float[] baseline = [37 / 100f, 12 / 100f, 7 / 20f, 19 / 20f, 2 / 3f, 3 / 8f,
            1f, 500000 / 999999, 5 / 100f, 12 / 100f, 31 / 100f, 44 / 100f, 99 / 100f, 100 / 100f];
        using var actual = new StatePreprocessorService().StateToTensor(state);
        Assert.Equal(baseline.Select(BitConverter.SingleToInt32Bits),
            actual.data<float>().ToArray().Select(BitConverter.SingleToInt32Bits));
    }
}