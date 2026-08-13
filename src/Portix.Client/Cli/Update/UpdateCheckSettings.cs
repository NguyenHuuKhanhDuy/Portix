using System.Text.Json;

namespace Portix.Client.Cli.Update;

/// <summary>
/// Persists whether `portix http`/`portix https` should check for and warn about newer releases
/// on startup. Kept separate from <see cref="ClientConfigStore"/> — that file is loaded as an
/// ASP.NET Core configuration source for the daemon and gets overwritten wholesale by
/// `portix login`/`logout`, so folding this CLI-only preference into it would mean logging in or
/// out silently resets it.
/// </summary>
public static class UpdateCheckSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Portix", "update-check.json");

    public static bool IsEnabled()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return true;
            }

            var stored = JsonSerializer.Deserialize<StoredSettings>(File.ReadAllText(SettingsPath));
            return stored?.Enabled ?? true;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        var directory = Path.GetDirectoryName(SettingsPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(new StoredSettings { Enabled = enabled }, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }

    private sealed class StoredSettings
    {
        public bool Enabled { get; set; } = true;
    }
}
