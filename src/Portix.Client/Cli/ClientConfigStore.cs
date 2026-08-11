using System.Text.Json;

namespace Portix.Client.Cli;

/// <summary>
/// Persists the personal API token (and server URL) set via <c>portix login</c> to a per-user
/// file outside the build/publish output, so it survives rebuilds and isn't tied to
/// <c>ASPNETCORE_ENVIRONMENT</c>. Loaded as the highest-precedence configuration source by the
/// daemon (see Program.cs), so it always overrides appsettings.json.
/// </summary>
public static class ClientConfigStore
{
    public static string ConfigPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Portix", "config.json");

    public static void Save(string token, string serverUrl)
    {
        var directory = Path.GetDirectoryName(ConfigPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(new
        {
            Portix = new { Token = token, ServerUrl = serverUrl },
        }, new JsonSerializerOptions { WriteIndented = true });

        File.WriteAllText(ConfigPath, json);
    }

    public static void Clear()
    {
        if (File.Exists(ConfigPath))
        {
            File.Delete(ConfigPath);
        }
    }
}
