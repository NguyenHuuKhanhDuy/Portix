using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Portix.Client.Api;
using Portix.Client.Tunneling;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Portix.Client.Cli.Commands;

public sealed class RestoreCommand : AsyncCommand<RestoreCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--api")]
        [Description("Daemon's local API base URL; omit to start a private, isolated daemon for this invocation")]
        public string? Api { get; init; }

        [CommandOption("--new")]
        [Description("Get a freshly assigned subdomain for every tunnel instead of trying to reuse its previous one")]
        public bool New { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var persisted = TunnelSessionStore.Load();
        if (persisted.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Nothing to restore.[/]");
            return 0;
        }

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

        var results = new List<RestoreResult>();
        var openedIds = new List<string>();

        foreach (var entry in persisted)
        {
            var requestedSubdomain = settings.New ? null : entry.Subdomain;
            var (tunnel, error, statusCode) = await TryOpenAsync(client, baseUri, entry, requestedSubdomain, cancellationToken).ConfigureAwait(false);

            var usedFallback = false;
            if (tunnel is null && requestedSubdomain is not null && statusCode == HttpStatusCode.BadRequest)
            {
                // The subdomain itself was rejected (already taken) — retry once with none requested.
                (tunnel, error, statusCode) = await TryOpenAsync(client, baseUri, entry, subdomain: null, cancellationToken).ConfigureAwait(false);
                usedFallback = tunnel is not null;
            }

            if (tunnel is not null)
            {
                openedIds.Add(tunnel.Id);
            }

            results.Add(new RestoreResult(entry, tunnel, tunnel is null ? error : null, usedFallback));
        }

        PrintResults(results);

        if (openedIds.Count == 0)
        {
            return 1;
        }

        BrowserLauncher.Open(baseUri);

        AnsiConsole.MarkupLine("[grey]Press Ctrl+C to close all restored tunnels.[/]");

        using var shutdownCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            shutdownCts.Cancel();
        };

        try
        {
            await Task.Delay(Timeout.Infinite, shutdownCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected — Ctrl+C.
        }

        AnsiConsole.MarkupLine("[grey]Closing tunnels...[/]");
        foreach (var id in openedIds)
        {
            using var deleteResponse = await client.DeleteAsync(new Uri(baseUri, $"/api/tunnels/{id}"), CancellationToken.None).ConfigureAwait(false);
            if (!deleteResponse.IsSuccessStatusCode && deleteResponse.StatusCode != HttpStatusCode.NotFound)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning: failed to close tunnel {id} cleanly ({deleteResponse.StatusCode}).[/]");
            }
        }

        return 0;
    }

    private static async Task<(TunnelDto? Tunnel, string? Error, HttpStatusCode StatusCode)> TryOpenAsync(
        HttpClient client, Uri baseUri, PersistedTunnel entry, string? subdomain, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(
            new Uri(baseUri, "/api/tunnels"),
            new { localPort = entry.LocalPort, subdomain, scheme = entry.Scheme },
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return (null, error, response.StatusCode);
        }

        var tunnel = await response.Content.ReadFromJsonAsync<TunnelDto>(cancellationToken: cancellationToken).ConfigureAwait(false);
        return (tunnel, null, response.StatusCode);
    }

    private static void PrintResults(IReadOnlyList<RestoreResult> results)
    {
        var table = new Table();
        table.AddColumn("Port");
        table.AddColumn("Scheme");
        table.AddColumn("Subdomain");
        table.AddColumn("Status");

        foreach (var result in results)
        {
            var status = result.Tunnel is not null
                ? (result.UsedFallback ? "[yellow]restored (new subdomain)[/]" : "[green]restored[/]")
                : $"[red]failed: {Markup.Escape(result.Error ?? "unknown error")}[/]";
            var subdomain = result.Tunnel?.Subdomain ?? result.Entry.Subdomain ?? "-";
            table.AddRow(result.Entry.LocalPort.ToString(), result.Entry.Scheme, subdomain, status);
        }

        AnsiConsole.Write(table);

        foreach (var result in results)
        {
            if (result.Tunnel?.PublicUrl is { } publicUrl)
            {
                AnsiConsole.MarkupLine($"[grey]{Markup.Escape(publicUrl)}[/]");
            }
        }
    }

    private sealed record RestoreResult(PersistedTunnel Entry, TunnelDto? Tunnel, string? Error, bool UsedFallback);
}
