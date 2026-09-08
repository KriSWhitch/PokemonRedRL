using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace PokemonRedRL.ControlPanel.Services;

/// <summary>Candidate top-level window that could be attached to a tile for preview capture.</summary>
public record CandidateWindow(IntPtr Handle, string Title, int ProcessId, string ProcessName);

/// <summary>
/// Enumerates candidate mGBA top-level windows so the user can explicitly pick which window
/// belongs to which tile. Deliberately does NOT auto-guess port-to-window mapping: the review
/// flagged "misleading dashboard" (wrong preview shown for a tile) as a real risk, and there is
/// no reliable OS-level signal linking a TCP port to a window handle, so correctness here depends
/// on an explicit user choice rather than a heuristic.
/// </summary>
public interface IWindowEnumerationService
{
    List<CandidateWindow> GetCandidateEmulatorWindows();
}

public class WindowEnumerationService : IWindowEnumerationService
{
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    public List<CandidateWindow> GetCandidateEmulatorWindows()
    {
        var results = new List<CandidateWindow>();

        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd)) return true;

            var length = GetWindowTextLength(hWnd);
            if (length == 0) return true;

            var builder = new StringBuilder(length + 1);
            GetWindowText(hWnd, builder, builder.Capacity);
            var title = builder.ToString();

            GetWindowThreadProcessId(hWnd, out var pid);

            string processName;
            try
            {
                processName = Process.GetProcessById((int)pid).ProcessName;
            }
            catch
            {
                return true;
            }

            if (processName.Contains("mgba", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new CandidateWindow(hWnd, title, (int)pid, processName));
            }

            return true;
        }, IntPtr.Zero);

        return results;
    }
}
