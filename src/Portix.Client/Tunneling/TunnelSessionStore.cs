using System.Text.Json;

namespace Portix.Client.Tunneling;

/// <summary>
/// Remembers opened tunnels to disk (keyed by local port) so `portix restore` can bring them
/// back later — an entry is only ever added or updated when a tunnel opens, never removed just
/// because it was closed (see Upsert). A tunnel opened via the CLI (`portix http`/`https`)
/// deliberately resets the file to just itself instead (see ResetTo) — typing a fresh command is
/// treated as "this is what I'm doing now," discarding whatever else was recorded, including
/// tunnels other still-running daemons currently have open. A tunnel opened via the dashboard's
/// "add tunnel" action only ever goes through Upsert, so it adds to the existing set instead.
/// </summary>
public static class TunnelSessionStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Portix", "last-session.json");

    // Guards against this process's own concurrent writes racing each other; does not (and can't,
    // without cross-process file locking) prevent a rare lost update if two separate daemon
    // processes write at the exact same instant — acceptable for a best-effort convenience file.
    private static readonly object WriteLock = new();

    /// <summary>Adds/updates the entry for <paramref name="tunnel"/>'s local port, leaving every other port's entry (including ones owned by other daemon processes) untouched.</summary>
    public static void Upsert(PersistedTunnel tunnel)
    {
        lock (WriteLock)
        {
            try
            {
                var current = LoadRaw();
                current.RemoveAll(t => t.LocalPort == tunnel.LocalPort);
                current.Add(tunnel);

                SaveRaw(current);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                // Best-effort — a failed read/write here shouldn't take down the daemon or an open tunnel.
            }
        }
    }

    /// <summary>Discards every existing entry and leaves the file with just <paramref name="tunnel"/> — used only for a CLI-initiated open (see class remarks), never for the dashboard or for `portix restore`'s own multi-entry reopening.</summary>
    public static void ResetTo(PersistedTunnel tunnel)
    {
        lock (WriteLock)
        {
            try
            {
                SaveRaw([tunnel]);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                // Best-effort — a failed write here shouldn't take down the CLI process or the tunnel it just opened.
            }
        }
    }

    public static IReadOnlyList<PersistedTunnel> Load()
    {
        try
        {
            return LoadRaw();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    private static List<PersistedTunnel> LoadRaw()
    {
        if (!File.Exists(FilePath))
        {
            return [];
        }

        var json = File.ReadAllText(FilePath);
        return JsonSerializer.Deserialize<List<PersistedTunnel>>(json) ?? [];
    }

    private static void SaveRaw(List<PersistedTunnel> tunnels)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(tunnels, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(FilePath, json);
    }
}
