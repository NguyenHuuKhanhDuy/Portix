using System.ComponentModel;
using System.Net.Http;
using Portix.Client.Cli.Update;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Portix.Client.Cli.Commands;

public sealed class UpdateCommand : AsyncCommand<UpdateCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--check")]
        [Description("Only report whether an update is available; don't install it")]
        public bool CheckOnly { get; init; }

        [CommandOption("--yes")]
        [Description("Skip the confirmation prompt before installing")]
        public bool Yes { get; init; }

        [CommandOption("--auto-check")]
        [Description("Enable or disable the automatic update notice shown when starting a tunnel (persisted, default: enabled)")]
        public bool? AutoCheck { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (settings.AutoCheck is not null)
        {
            UpdateCheckSettings.SetEnabled(settings.AutoCheck.Value);
            AnsiConsole.MarkupLine(settings.AutoCheck.Value
                ? "[green]Automatic update notices enabled — 'portix http'/'portix https' will check for new versions on start.[/]"
                : "[yellow]Automatic update notices disabled.[/]");
            return 0;
        }

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        GitHubRelease? release;
        try
        {
            release = await GitHubReleaseClient.GetLatestReleaseAsync(client, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: could not reach GitHub to check for updates ({Markup.Escape(ex.Message)}).[/]");
            return 1;
        }

        if (release is null)
        {
            AnsiConsole.MarkupLine("[yellow]No releases have been published yet.[/]");
            return 0;
        }

        var latestVersion = ReleaseVersion.Parse(release.TagName);
        if (latestVersion is null)
        {
            AnsiConsole.MarkupLine($"[red]Error: could not parse the latest release's version ('{Markup.Escape(release.TagName)}').[/]");
            return 1;
        }

        var currentVersion = ReleaseVersion.Parse(VersionInfo.Current);
        if (currentVersion is not null && latestVersion <= currentVersion)
        {
            AnsiConsole.MarkupLine($"[green]Already up to date (v{VersionInfo.Current}).[/]");
            return 0;
        }

        AnsiConsole.MarkupLine($"[cyan]Update available: v{VersionInfo.Current} -> {Markup.Escape(release.TagName)}[/]");

        if (settings.CheckOnly)
        {
            return 0;
        }

        var assetName = PlatformAsset.CurrentAssetName();
        if (assetName is null)
        {
            AnsiConsole.MarkupLine("[red]Error: there is no published build for this operating system/architecture.[/]");
            return 1;
        }

        var asset = release.FindAsset(assetName);
        var checksumAsset = release.FindAsset($"{assetName}.sha256");
        if (asset is null || checksumAsset is null)
        {
            AnsiConsole.MarkupLine($"[red]Error: release {Markup.Escape(release.TagName)} is missing the '{assetName}' asset or its checksum file.[/]");
            return 1;
        }

        if (!settings.Yes && !AnsiConsole.Confirm($"Install {Markup.Escape(release.TagName)}?"))
        {
            AnsiConsole.MarkupLine("[grey]Update cancelled.[/]");
            return 0;
        }

        AnsiConsole.MarkupLine("[grey]Downloading update...[/]");
        byte[] newExecutableBytes;
        try
        {
            var zipBytes = await ExecutableUpdater.DownloadAndVerifyAsync(
                client, asset.BrowserDownloadUrl, checksumAsset.BrowserDownloadUrl, cancellationToken).ConfigureAwait(false);
            newExecutableBytes = ExecutableUpdater.ExtractExecutableFromZip(zipBytes);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
        {
            AnsiConsole.MarkupLine($"[red]Error: {Markup.Escape(ex.Message)}[/]");
            return 1;
        }

        AnsiConsole.MarkupLine("[grey]Stopping the local daemon (if running)...[/]");
        await ExecutableUpdater.StopRunningDaemonAsync(client, cancellationToken).ConfigureAwait(false);

        try
        {
            ExecutableUpdater.ReplaceCurrentExecutable(newExecutableBytes);
        }
        catch (ExecutableInUseException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {Markup.Escape(ex.Message)}[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"[green]Updated to {Markup.Escape(release.TagName)}.[/]");
        return 0;
    }
}
