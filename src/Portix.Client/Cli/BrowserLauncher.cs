using System.Diagnostics;

namespace Portix.Client.Cli;

public static class BrowserLauncher
{
    // Best-effort only — a browser that can't be launched (headless box, unusual environment,
    // no default handler registered) must never fail tunnel startup.
    public static void Open(Uri url)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo(url.ToString()) { UseShellExecute = true });
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", url.ToString());
            }
        }
        catch
        {
            // Ignored — see comment above.
        }
    }
}
