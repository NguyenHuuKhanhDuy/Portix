using System.Net.Http;
using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace Portix.Client.Cli.Update;

/// <summary>Thrown when the running executable can't be replaced because another Portix process still has it open (e.g. an active tunnel session).</summary>
public sealed class ExecutableInUseException(string message, Exception inner) : Exception(message, inner);

public static class ExecutableUpdater
{
    private static readonly Uri DaemonBaseUri = new("http://127.0.0.1:4040");
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ShutdownBudget = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ShutdownPollInterval = TimeSpan.FromMilliseconds(300);

    /// <summary>Downloads the asset and its published checksum, verifying the two match before returning the bytes.</summary>
    public static async Task<byte[]> DownloadAndVerifyAsync(HttpClient client, string assetUrl, string checksumUrl, CancellationToken cancellationToken)
    {
        var bytes = await client.GetByteArrayAsync(assetUrl, cancellationToken).ConfigureAwait(false);
        var checksumFile = await client.GetStringAsync(checksumUrl, cancellationToken).ConfigureAwait(false);
        var expectedHash = checksumFile.Split(' ', 2)[0].Trim();

        var actualHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Checksum mismatch for the downloaded update (expected {expectedHash}, got {actualHash}). Aborting — nothing was installed.");
        }

        return bytes;
    }

    /// <summary>Asks the local daemon (if reachable) to shut down, and waits for it to actually stop before returning.</summary>
    public static async Task StopRunningDaemonAsync(HttpClient client, CancellationToken cancellationToken)
    {
        if (!await IsReachableAsync(client, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            using var response = await client.PostAsync(new Uri(DaemonBaseUri, "/api/shutdown"), content: null, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            // Best-effort — the reachability poll below is what actually governs how long we wait.
        }

        using var timeoutCts = new CancellationTokenSource(ShutdownBudget);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);
        while (await IsReachableAsync(client, CancellationToken.None).ConfigureAwait(false))
        {
            try
            {
                await Task.Delay(ShutdownPollInterval, linkedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private static async Task<bool> IsReachableAsync(HttpClient client, CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(ProbeTimeout);
            using var response = await client.GetAsync(new Uri(DaemonBaseUri, "/api/tunnels"), cts.Token).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Replaces the currently running executable's file on disk with <paramref name="newExecutableBytes"/>.</summary>
    /// <exception cref="ExecutableInUseException">The file is still held open by another Portix process (e.g. an active tunnel session).</exception>
    public static void ReplaceCurrentExecutable(byte[] newExecutableBytes)
    {
        var currentPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Could not determine the current executable's path.");

        try
        {
            if (OperatingSystem.IsWindows())
            {
                ReplaceOnWindows(currentPath, newExecutableBytes);
            }
            else
            {
                ReplaceOnUnix(currentPath, newExecutableBytes);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ExecutableInUseException(
                "Could not replace the running executable — it looks like it's still in use (e.g. an active 'portix http'/'portix https' session). Close any open tunnels and try again.",
                ex);
        }
    }

    // Windows won't let you overwrite a running exe's bytes in place, but it does allow renaming
    // it while it's executing (verified directly: a self-contained single-file apphost can be
    // renamed out from under its own running process, which keeps serving from the renamed file
    // without interruption). So: rename the live exe aside, move the new one into the live name,
    // then best-effort delete the renamed original — deleting it can fail while its process is
    // still running, which is fine; it's cleaned up by the next successful update.
    private static void ReplaceOnWindows(string currentPath, byte[] newExecutableBytes)
    {
        var oldPath = currentPath + ".old";
        var newPath = currentPath + ".new";

        File.WriteAllBytes(newPath, newExecutableBytes);
        TryDelete(oldPath);

        File.Move(currentPath, oldPath);
        File.Move(newPath, currentPath);

        TryDelete(oldPath);
    }

    // Unix lets you replace a file that a running process still has open — the process keeps its
    // existing inode; only the next process to open the path sees the new content — so a direct
    // overwrite works. The downloaded bytes don't carry the executable bit, so set it explicitly.
    [UnsupportedOSPlatform("windows")]
    private static void ReplaceOnUnix(string currentPath, byte[] newExecutableBytes)
    {
        var newPath = currentPath + ".new";
        File.WriteAllBytes(newPath, newExecutableBytes);
        File.SetUnixFileMode(newPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        File.Move(newPath, currentPath, overwrite: true);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Leftover from a previous update, or still held open by a still-running old process —
            // harmless either way; cleaned up by the next successful update.
        }
    }
}
