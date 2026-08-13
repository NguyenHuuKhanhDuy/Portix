using System.Diagnostics;
using System.Net.Http;
using System.Reflection;

namespace Portix.Client.Cli;

/// <summary>Probes whether the daemon's local API is reachable, and self-relaunches (detached) as the daemon if not.</summary>
public static class DaemonLauncher
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan StartupBudget = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    public static async Task EnsureRunningAsync(HttpClient client, Uri baseUri)
    {
        if (await IsReachableAsync(client, baseUri).ConfigureAwait(false))
        {
            return;
        }

        var process = StartSelfDetached();
        if (OperatingSystem.IsWindows() && process is not null)
        {
            Win32JobObject.AssignToNewJob(process);
        }

        using var timeoutCts = new CancellationTokenSource(StartupBudget);
        while (!timeoutCts.IsCancellationRequested)
        {
            if (await IsReachableAsync(client, baseUri).ConfigureAwait(false))
            {
                return;
            }

            try
            {
                await Task.Delay(PollInterval, timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        throw new TimeoutException("Started the daemon, but its local API did not become reachable in time.");
    }

    /// <summary>
    /// Starts a fresh, private daemon dedicated to this CLI invocation: an ephemeral local API port
    /// (so concurrent isolated daemons don't collide) and, on Windows, a Job Object tying the
    /// daemon's lifetime to this process so it's killed however this process ends. Other platforms
    /// fall back to today's shared, fixed-port, non-isolated daemon behavior.
    /// </summary>
    public static async Task<Uri> StartIsolatedAsync(HttpClient client, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            var fallbackUri = new Uri("http://127.0.0.1:4040");
            await EnsureRunningAsync(client, fallbackUri).ConfigureAwait(false);
            return fallbackUri;
        }

        var announceFilePath = Path.Combine(Path.GetTempPath(), $"portix-port-{Guid.NewGuid():N}.txt");

        var (fileName, arguments) = GetSelfRelaunchCommand();
        var startInfo = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = AppContext.BaseDirectory,
        };

        if (arguments is not null)
        {
            startInfo.Arguments = arguments;
        }

        // Prefer a fixed, memorable port so the dashboard URL is stable across runs; only a
        // second concurrent isolated daemon (another tunnel already open) needs to fall back to
        // an OS-assigned one, which is what PORTIX_ALLOW_PORT_FALLBACK tells Program.cs to do
        // rather than fail outright when this preferred port is already taken.
        startInfo.Environment["Portix__LocalApiPort"] = "4041";
        startInfo.Environment["PORTIX_ALLOW_PORT_FALLBACK"] = "1";
        startInfo.Environment["PORTIX_PORT_ANNOUNCE_FILE"] = announceFilePath;
        startInfo.Environment["PORTIX_RUN_DAEMON"] = "1";

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start an isolated daemon process.");
        Win32JobObject.AssignToNewJob(process);

        try
        {
            using var timeoutCts = new CancellationTokenSource(StartupBudget);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

            while (!File.Exists(announceFilePath))
            {
                try
                {
                    await Task.Delay(PollInterval, linkedCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            if (!File.Exists(announceFilePath))
            {
                throw new TimeoutException("Started an isolated daemon, but it did not announce its local API port in time.");
            }

            var portText = await File.ReadAllTextAsync(announceFilePath, CancellationToken.None).ConfigureAwait(false);
            return new Uri($"http://127.0.0.1:{portText.Trim()}");
        }
        finally
        {
            try
            {
                File.Delete(announceFilePath);
            }
            catch (IOException)
            {
                // Best-effort cleanup; a leftover temp file is harmless.
            }
        }
    }

    private static async Task<bool> IsReachableAsync(HttpClient client, Uri baseUri)
    {
        try
        {
            using var cts = new CancellationTokenSource(ProbeTimeout);
            using var response = await client.GetAsync(new Uri(baseUri, "/api/tunnels"), cts.Token).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Relaunches this same executable, detached, with no arguments — which is exactly what makes
    /// it start as the daemon (see Program.cs's dispatch). No sibling binary to locate: whatever
    /// process is currently running IS the thing that needs to run again as the daemon.
    /// </summary>
    private static Process? StartSelfDetached()
    {
        var (fileName, arguments) = GetSelfRelaunchCommand();

        var startInfo = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            // Without this, the child inherits whatever directory the user happened to invoke the
            // CLI from, and ASP.NET Core resolves ContentRootPath (appsettings.json, wwwroot) from
            // the process's current directory by default — not from where the binary itself lives.
            WorkingDirectory = AppContext.BaseDirectory,
        };

        if (arguments is not null)
        {
            startInfo.Arguments = arguments;
        }

        // The only thing that makes this relaunch start the daemon instead of going back through
        // the CLI framework (see Program.cs) — never set for anything a human could type directly.
        startInfo.Environment["PORTIX_RUN_DAEMON"] = "1";

        return Process.Start(startInfo);
    }

    private static (string FileName, string? Arguments) GetSelfRelaunchCommand()
    {
        var processPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Could not determine the current executable's path to self-relaunch.");

        // Running via `dotnet <dll>` (this repo's dev/test workflow): ProcessPath is the dotnet
        // muxer itself, not our assembly, so relaunching it bare would just run the SDK's own
        // default behavior. Relaunch as `dotnet "<entry-assembly-dll>"` instead.
        if (Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            var dllPath = Assembly.GetEntryAssembly()?.Location
                ?? throw new InvalidOperationException("Could not determine the entry assembly's path to self-relaunch via dotnet.");
            return (processPath, $"\"{dllPath}\"");
        }

        // Native apphost (published exe, or `dotnet run`'s produced binary): relaunch it directly.
        return (processPath, null);
    }
}
