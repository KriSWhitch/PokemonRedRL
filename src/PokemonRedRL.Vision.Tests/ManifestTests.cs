using PokemonRedRL.ControlPanel.Models;
using PokemonRedRL.ControlPanel.Services;
using Xunit;

namespace PokemonRedRL.Vision.Tests;

public class ManifestTests
{
    [Fact]
    public void AttachedIdentityPersistsAndAtomicSaveLeavesNoTemporaryFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var service = new RunManifestService();
            var manifest = service.CreateRun(directory, "fixture.gb", 2, 12345,
                LaunchMode.ManualAttach, SpeedProfile.Normal);
            var path = Path.Combine(manifest.RunDirectory, "manifest.json");
            manifest.Agents[1].EmulatorProcessId = 102;
            manifest.Agents[1].WindowHandle = 222;
            service.Save(manifest);
            var loaded = service.Load(path);
            Assert.Equal(222, loaded.Agents[1].WindowHandle);
            var binding = FrameBinding.Load(path, 1, "127.0.0.1", 12346, new FrameRegion(0, 0, 160, 144));
            Assert.Equal(102, binding.ProcessId);
            Assert.Empty(Directory.GetFiles(manifest.RunDirectory, "*.tmp"));
        }
        finally { Directory.Delete(directory, true); }
    }
}