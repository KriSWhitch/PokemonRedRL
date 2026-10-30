using System.Text.Json;

namespace PokemonRedRL.Vision;

public sealed record FrameBinding(string RunId, int Index, string Host, int Port, int ProcessId,
    long WindowHandle, FrameRegion Region)
{
    public static FrameBinding Load(string manifestPath, int index, string host, int port, FrameRegion region)
    {
        if (!Path.IsPathFullyQualified(manifestPath))
            throw new ArgumentException("Manifest path must be absolute.", nameof(manifestPath));
        using var stream = new FileStream(manifestPath, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        return Parse(stream, index, host, port, region);
    }

    public static FrameBinding Parse(Stream manifest, int index, string host, int port, FrameRegion region)
    {
        using var document = JsonDocument.Parse(manifest);
        var root = document.RootElement;
        var runId = root.GetProperty("RunId").GetString();
        if (string.IsNullOrWhiteSpace(runId)) throw new InvalidDataException("Manifest RunId is missing.");
        var slots = root.GetProperty("Agents").EnumerateArray().ToArray();
        if (slots.GroupBy(slot => slot.GetProperty("Index").GetInt32()).Any(group => group.Count() != 1))
            throw new InvalidDataException("Manifest contains duplicate slot indices.");
        var matches = slots.Where(slot => slot.GetProperty("Index").GetInt32() == index).ToArray();
        if (matches.Length != 1) throw new InvalidDataException("Requested slot is missing or ambiguous.");
        var selected = matches[0];
        var selectedHost = selected.GetProperty("Host").GetString();
        var selectedPort = selected.GetProperty("Port").GetInt32();
        if (!string.Equals(host, selectedHost, StringComparison.OrdinalIgnoreCase) || selectedPort != port ||
            port is < 1 or > 65535)
            throw new InvalidDataException("Manifest slot does not match the requested host/port.");
        if (slots.Count(slot => string.Equals(slot.GetProperty("Host").GetString(), host,
                StringComparison.OrdinalIgnoreCase) && slot.GetProperty("Port").GetInt32() == port) != 1)
            throw new InvalidDataException("Manifest contains an ambiguous host/port association.");
        if (!selected.TryGetProperty("EmulatorProcessId", out var pid) || pid.ValueKind != JsonValueKind.Number ||
            !pid.TryGetInt32(out var processId) || processId <= 0 ||
            !selected.TryGetProperty("WindowHandle", out var hwnd) || hwnd.ValueKind != JsonValueKind.Number ||
            !hwnd.TryGetInt64(out var handle) || handle == 0)
            throw new InvalidDataException("Attach and save the emulator PID/window before diagnostics.");
        if (!region.Fits(int.MaxValue, int.MaxValue))
            throw new InvalidDataException("An explicit positive game viewport is required.");
        return new FrameBinding(runId, index, host, port, processId, handle, region);
    }
}