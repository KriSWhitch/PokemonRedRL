using System.Diagnostics;
using System.IO;
using PokemonRedRL.ControlPanel.Models;

namespace PokemonRedRL.ControlPanel.Services;

public class AgentLogLineEventArgs : EventArgs
{
    public required int AgentIndex { get; init; }
    public required string Line { get; init; }
    public AgentTelemetry? Telemetry { get; init; }
}

public class AgentExitedEventArgs : EventArgs
{
    public required int AgentIndex { get; init; }
    public required int ExitCode { get; init; }
}

/// <summary>
/// Launches and supervises one PokemonRedRL.Agent process per slot (Step 2 decision:
/// one-process-per-agent). Streams stdout so the control panel can show per-tile logs and
/// parsed telemetry without any new IPC channel — every line from a given process belongs to
/// exactly that agent, so attribution is exact.
/// </summary>
public interface IAgentProcessManager
{
    event EventHandler<AgentLogLineEventArgs>? LogLineReceived;
    event EventHandler<AgentExitedEventArgs>? AgentExited;

    bool StartAgent(AgentSlot slot);
    void StopAgent(int agentIndex);
    void StopAll();
}

public class AgentProcessManager : IAgentProcessManager, IDisposable
{
    private readonly Dictionary<int, Process> _processes = new();

    public event EventHandler<AgentLogLineEventArgs>? LogLineReceived;
    public event EventHandler<AgentExitedEventArgs>? AgentExited;

    public bool StartAgent(AgentSlot slot)
    {
        var agentDllPath = LocateAgentDll();
        if (agentDllPath is null)
        {
            LogLineReceived?.Invoke(this, new AgentLogLineEventArgs
            {
                AgentIndex = slot.Index,
                Line = "ERROR: could not locate a built PokemonRedRL.Agent.dll. Build the solution first."
            });
            return false;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{agentDllPath}\" --port {slot.Port} --agent-index {slot.Index}",
            WorkingDirectory = slot.RuntimeDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => HandleLine(slot.Index, e.Data);
        process.ErrorDataReceived += (_, e) => HandleLine(slot.Index, e.Data);
        process.Exited += (_, _) =>
        {
            // Exited fires on a ThreadPool wait-handle callback; it can still be in flight when
            // StopAgent disposes this same Process, so guard against reading a disposed instance.
            try
            {
                AgentExited?.Invoke(this, new AgentExitedEventArgs { AgentIndex = slot.Index, ExitCode = process.ExitCode });
            }
            catch (InvalidOperationException)
            {
            }
        };

        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            process.Dispose();
            LogLineReceived?.Invoke(this, new AgentLogLineEventArgs
            {
                AgentIndex = slot.Index,
                Line = $"ERROR: failed to start agent process: {ex.Message}"
            });
            return false;
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        _processes[slot.Index] = process;
        slot.AgentProcessId = process.Id;
        return true;
    }

    public void StopAgent(int agentIndex)
    {
        if (!_processes.TryGetValue(agentIndex, out var process)) return;
        _processes.Remove(agentIndex);

        // Stop watching for exit before Kill()/Dispose() — narrows (but per the Exited handler's
        // own try/catch, doesn't have to fully eliminate) the race between the process's own
        // Exited callback and disposing this same Process instance below.
        process.EnableRaisingEvents = false;

        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Process already exited, or the OS refused the kill (e.g. access denied); nothing more we can do.
            LogLineReceived?.Invoke(this, new AgentLogLineEventArgs
            {
                AgentIndex = agentIndex,
                Line = $"WARN: could not stop agent process cleanly: {ex.Message}"
            });
        }
        finally
        {
            process.Dispose();
        }
    }

    public void StopAll()
    {
        foreach (var agentIndex in _processes.Keys.ToList())
        {
            StopAgent(agentIndex);
        }
    }

    private void HandleLine(int agentIndex, string? line)
    {
        if (string.IsNullOrEmpty(line)) return;

        AgentTelemetryParser.TryParse(line, out var telemetry);
        LogLineReceived?.Invoke(this, new AgentLogLineEventArgs
        {
            AgentIndex = agentIndex,
            Line = line,
            Telemetry = telemetry
        });
    }

    /// <summary>
    /// Finds the built PokemonRedRL.Agent.dll by walking up from this process's base directory to
    /// the solution root, then searching that project's bin folder. This assumes the solution has
    /// already been built (e.g. via `dotnet build src/PokemonRedRL.sln`) — the control panel does
    /// not build the agent itself.
    /// </summary>
    private static string? LocateAgentDll()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !dir.GetFiles("PokemonRedRL.sln").Any())
        {
            dir = dir.Parent;
        }
        if (dir is null) return null;

        var agentProjectDir = Path.Combine(dir.FullName, "PokemonRedRL.Agent");
        if (!Directory.Exists(agentProjectDir)) return null;

        return Directory.EnumerateFiles(agentProjectDir, "PokemonRedRL.Agent.dll", SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    public void Dispose() => StopAll();
}
