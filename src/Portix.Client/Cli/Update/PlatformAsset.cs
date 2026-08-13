using System.Runtime.InteropServices;

namespace Portix.Client.Cli.Update;

public static class PlatformAsset
{
    /// <summary>
    /// The release asset name for the OS/architecture this process is running on, or null if this
    /// platform has no published build (see scripts/publish-portix-client.ps1). Each asset is a
    /// zip containing the correctly-named `portix.exe`/`portix` — not the bare executable — so a
    /// user who downloads and extracts one gets a runnable binary immediately, with no rename step.
    /// </summary>
    public static string? CurrentAssetName()
    {
        if (OperatingSystem.IsWindows())
        {
            return RuntimeInformation.ProcessArchitecture == Architecture.X64 ? "portix-win-x64.zip" : null;
        }

        if (OperatingSystem.IsMacOS())
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "portix-osx-x64.zip",
                Architecture.Arm64 => "portix-osx-arm64.zip",
                _ => null,
            };
        }

        return null;
    }
}
