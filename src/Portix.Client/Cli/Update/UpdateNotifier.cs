using System.Net.Http;
using Spectre.Console;

namespace Portix.Client.Cli.Update;

/// <summary>
/// Best-effort "a new version is available" notice printed when opening a tunnel. Must never
/// throw or meaningfully delay tunnel startup — every failure (disabled, offline, GitHub down,
/// slow network) is swallowed.
/// </summary>
public static class UpdateNotifier
{
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(2);

    public static async Task NotifyIfUpdateAvailableAsync(CancellationToken cancellationToken)
    {
        if (!UpdateCheckSettings.IsEnabled())
        {
            return;
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(CheckTimeout);

            using var client = new HttpClient();
            var release = await GitHubReleaseClient.GetLatestReleaseAsync(client, cts.Token).ConfigureAwait(false);
            if (release is null)
            {
                return;
            }

            var latestVersion = ReleaseVersion.Parse(release.TagName);
            var currentVersion = ReleaseVersion.Parse(VersionInfo.Current);
            if (latestVersion is null || (currentVersion is not null && latestVersion <= currentVersion))
            {
                return;
            }

            AnsiConsole.MarkupLine(
                $"[yellow]A new version of Portix is available: {Markup.Escape(release.TagName)} (you're on v{VersionInfo.Current}). Run 'portix update' to upgrade.[/]");
        }
        catch
        {
            // Best-effort only — an update check must never disrupt opening a tunnel.
        }
    }
}
