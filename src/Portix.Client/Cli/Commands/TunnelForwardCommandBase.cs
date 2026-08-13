using System.ComponentModel;
using System.Net.Http;
using System.Net.Http.Json;
using Portix.Client.Api;
using Portix.Client.Cli.Update;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Portix.Client.Cli.Commands;

/// <summary>
/// Shared implementation for `portix http`/`portix https`: they differ only in which scheme
/// the daemon should use to reach the local app, via <see cref="Scheme"/>.
/// </summary>
public abstract class TunnelForwardCommandBase : AsyncCommand<TunnelForwardCommandBase.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<port>")]
        [Description("Local port to expose, e.g. 3000")]
        public int Port { get; init; }

        [CommandOption("--subdomain")]
        [Description("Requested subdomain; omit for a random one")]
        public string? Subdomain { get; init; }

        [CommandOption("--api")]
        [Description("Daemon's local API base URL; omit to start a private, isolated daemon for this invocation")]
        public string? Api { get; init; }
    }

    /// <summary>Scheme the daemon should use to reach the local app: "http" or "https".</summary>
    protected abstract string Scheme { get; }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        Uri baseUri;
        if (settings.Api is not null)
        {
            baseUri = new Uri(settings.Api);
            AnsiConsole.MarkupLine("[grey]Checking for a running daemon...[/]");
            await DaemonLauncher.EnsureRunningAsync(client, baseUri).ConfigureAwait(false);
        }
        else
        {
            AnsiConsole.MarkupLine("[grey]Starting an isolated daemon for this session...[/]");
            baseUri = await DaemonLauncher.StartIsolatedAsync(client, cancellationToken).ConfigureAwait(false);
        }

        using var response = await client.PostAsJsonAsync(
            new Uri(baseUri, "/api/tunnels"),
            new { localPort = settings.Port, subdomain = settings.Subdomain, scheme = Scheme },
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            AnsiConsole.MarkupLine($"[red]Error: could not open tunnel: {Markup.Escape(error)}[/]");
            return 1;
        }

        var tunnel = await response.Content.ReadFromJsonAsync<TunnelDto>(cancellationToken: cancellationToken).ConfigureAwait(false);
        if (tunnel is null)
        {
            AnsiConsole.MarkupLine("[red]Error: malformed response from daemon.[/]");
            return 1;
        }

        // Best-effort only, and must run before the live dashboard below takes over the terminal —
        // interleaving plain MarkupLine output with AnsiConsole.Live's rendering would corrupt it.
        await UpdateNotifier.NotifyIfUpdateAvailableAsync(cancellationToken).ConfigureAwait(false);

        AnsiConsole.MarkupLine("[grey]Press Ctrl+C to close this tunnel.[/]");

        using var shutdownCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            shutdownCts.Cancel();
        };

        await LiveTunnelDashboard.RunAsync(baseUri, tunnel, shutdownCts.Token).ConfigureAwait(false);

        AnsiConsole.MarkupLine("[grey]Closing tunnel...[/]");
        using var deleteResponse = await client.DeleteAsync(new Uri(baseUri, $"/api/tunnels/{tunnel.Id}"), CancellationToken.None).ConfigureAwait(false);
        if (!deleteResponse.IsSuccessStatusCode && deleteResponse.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            AnsiConsole.MarkupLine($"[yellow]Warning: failed to close tunnel cleanly ({deleteResponse.StatusCode}).[/]");
        }

        return 0;
    }
}
