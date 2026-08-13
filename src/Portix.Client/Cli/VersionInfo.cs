using System.Reflection;

namespace Portix.Client.Cli;

/// <summary>The version embedded at publish time via `dotnet publish -p:Version=...` (see scripts/publish-portix-client.ps1).</summary>
public static class VersionInfo
{
    public static string Current { get; } = ReadVersion();

    private static string ReadVersion()
    {
        // The SDK always generates this attribute, even without -p:Version — it defaults to
        // "1.0.0" and, in a git checkout, appends "+<commit-sha>" (SourceRevisionId). Strip that
        // suffix so display and version-comparison logic never has to deal with it; fall back to
        // a literal "0.0.0-dev" only in the (currently theoretical) case the attribute is absent.
        var informationalVersion =
            Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? "0.0.0-dev";

        var plusIndex = informationalVersion.IndexOf('+');
        return plusIndex >= 0 ? informationalVersion[..plusIndex] : informationalVersion;
    }
}
