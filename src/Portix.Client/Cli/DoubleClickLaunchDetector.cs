using System.Runtime.InteropServices;

namespace Portix.Client.Cli;

/// <summary>
/// Detects whether this process is the only one attached to its console — the signature of
/// Windows Explorer creating a brand-new console for a double-clicked .exe, as opposed to being
/// typed into a shell (cmd/PowerShell/Windows Terminal) that's already attached to that console
/// alongside it.
/// </summary>
public static class DoubleClickLaunchDetector
{
    // A console realistically never has anywhere close to this many processes attached, so this
    // never hits the "buffer too small" failure mode in practice.
    private const int ProbeBufferSize = 64;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetConsoleProcessList(uint[] processList, uint processCount);

    /// <summary>
    /// True only when exactly one process (this one) is attached to the current console. Any
    /// other outcome — a real count greater than one, or zero (no console at all, e.g. output
    /// redirected, or an unexpected API failure) — returns false, the safe default for an
    /// ambiguous or non-interactive invocation.
    /// </summary>
    public static bool IsLikelyDoubleClicked()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        var buffer = new uint[ProbeBufferSize];
        var count = GetConsoleProcessList(buffer, ProbeBufferSize);
        return count == 1;
    }
}
