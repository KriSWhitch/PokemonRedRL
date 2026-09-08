using System.IO;
using System.Text.Json;
using PokemonRedRL.ControlPanel.Models;

namespace PokemonRedRL.ControlPanel.Services;

public interface IRunManifestService
{
    /// <summary>Creates a fresh run directory + manifest for the given settings and one slot per agent.</summary>
    RunManifest CreateRun(string rootDirectory, string romPath, int agentCount, int basePort,
        LaunchMode launchMode, SpeedProfile speedProfile);

    void Save(RunManifest manifest);

    RunManifest Load(string manifestPath);
}

public class RunManifestService : IRunManifestService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public RunManifest CreateRun(string rootDirectory, string romPath, int agentCount, int basePort,
        LaunchMode launchMode, SpeedProfile speedProfile)
    {
        var runId = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var runDirectory = Path.Combine(rootDirectory, runId);
        Directory.CreateDirectory(runDirectory);

        var manifest = new RunManifest
        {
            RunId = runId,
            CreatedUtc = DateTime.UtcNow,
            RomPath = romPath,
            LaunchMode = launchMode,
            SpeedProfile = speedProfile,
            RunDirectory = runDirectory
        };

        for (var i = 0; i < agentCount; i++)
        {
            var slotDirectory = Path.Combine(runDirectory, $"agent-{i}");
            Directory.CreateDirectory(slotDirectory);

            manifest.Agents.Add(new AgentSlot
            {
                Index = i,
                Port = basePort + i,
                RuntimeDirectory = slotDirectory,
                Status = AgentSlotStatus.Pending
            });
        }

        Save(manifest);
        return manifest;
    }

    public void Save(RunManifest manifest)
    {
        var path = Path.Combine(manifest.RunDirectory, "manifest.json");
        File.WriteAllText(path, JsonSerializer.Serialize(manifest, JsonOptions));
    }

    public RunManifest Load(string manifestPath)
    {
        var json = File.ReadAllText(manifestPath);
        return JsonSerializer.Deserialize<RunManifest>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to parse manifest at {manifestPath}");
    }
}
