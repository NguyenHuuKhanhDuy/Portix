using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR.Client;
using Portix.Client.Api;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Portix.Client.Cli;

/// <summary>
/// Renders a colored status panel plus a live-updating request-log table for one tunnel,
/// sourced entirely from the daemon's existing local API/SignalR broadcast — the same channel
/// the web dashboard already uses. Runs until <paramref name="shutdownToken"/> is cancelled
/// (e.g. Ctrl+C).
/// </summary>
public static class LiveTunnelDashboard
{
    private const int MaxRequestRows = 30;
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(250);

    public static async Task RunAsync(Uri baseUri, TunnelDto tunnel, CancellationToken shutdownToken)
    {
        var state = new object();
        var status = tunnel.Status;
        var publicUrl = tunnel.PublicUrl;
        var requests = new List<CapturedRequestSummaryDto>();

        var hub = new HubConnectionBuilder()
            .WithUrl(new Uri(baseUri, "/hubs/dashboard"))
            .WithAutomaticReconnect()
            .Build();

        hub.On<CapturedRequestSummaryDto>("RequestCaptured", capture =>
        {
            if (capture.TunnelId != tunnel.Id)
            {
                return;
            }

            lock (state)
            {
                requests.Insert(0, capture);
                if (requests.Count > MaxRequestRows)
                {
                    requests.RemoveRange(MaxRequestRows, requests.Count - MaxRequestRows);
                }
            }
        });

        hub.On<TunnelDto>("TunnelStatusChanged", changed =>
        {
            if (changed.Id != tunnel.Id)
            {
                return;
            }

            lock (state)
            {
                status = changed.Status;
                publicUrl = changed.PublicUrl;
            }
        });

        try
        {
            await hub.StartAsync(shutdownToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The live view still works without the hub connected — it just won't update
            // beyond the initial snapshot. Not worth failing the whole command over.
        }

        IRenderable BuildRenderable()
        {
            lock (state)
            {
                return Render(baseUri, tunnel, status, publicUrl, requests);
            }
        }

        await AnsiConsole.Live(BuildRenderable())
            .StartAsync(async ctx =>
            {
                while (!shutdownToken.IsCancellationRequested)
                {
                    ctx.UpdateTarget(BuildRenderable());
                    ctx.Refresh();

                    try
                    {
                        await Task.Delay(RefreshInterval, shutdownToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            })
            .ConfigureAwait(false);

        await hub.DisposeAsync().ConfigureAwait(false);
    }

    private static IRenderable Render(Uri baseUri, TunnelDto tunnel, string status, string? publicUrl, IReadOnlyList<CapturedRequestSummaryDto> requests)
    {
        var statusPanel = new Table().Border(TableBorder.Rounded).HideHeaders().Expand();
        statusPanel.AddColumn(new TableColumn(""));
        statusPanel.AddColumn(new TableColumn(""));
        statusPanel.AddRow(new Markup("[bold]Session Status[/]"), new Markup($"[{StatusColor(status)}]{status.ToLowerInvariant()}[/]"));
        statusPanel.AddRow(new Markup("[bold]Web Interface[/]"), new Text(baseUri.ToString()));
        statusPanel.AddRow(new Markup("[bold]Forwarding[/]"), new Text($"{publicUrl ?? "(pending)"} -> {tunnel.Scheme}://localhost:{tunnel.LocalPort}"));

        var requestTable = new Table().Border(TableBorder.Rounded).Title("HTTP Requests").Expand();
        requestTable.AddColumn("Time");
        requestTable.AddColumn("Method");
        requestTable.AddColumn("Path");
        requestTable.AddColumn("Status");

        foreach (var r in requests)
        {
            requestTable.AddRow(
                new Text(r.Timestamp.ToLocalTime().ToString("HH:mm:ss")),
                new Text(r.Method),
                new Text(r.Path),
                new Markup($"[{StatusColor(r.StatusCode)}]{r.StatusCode} {ReasonPhrase(r.StatusCode)}[/]"));
        }

        if (requests.Count == 0)
        {
            requestTable.AddRow(new Text(""), new Text(""), new Text("Waiting for requests...", new Style(Color.Grey)), new Text(""));
        }

        return new Rows(statusPanel, requestTable);
    }

    private static string StatusColor(string status) => status switch
    {
        "Online" => "green",
        "Connecting" => "yellow",
        "Error" => "red",
        _ => "grey",
    };

    private static string StatusColor(int statusCode) => statusCode switch
    {
        >= 200 and < 300 => "green",
        >= 300 and < 400 => "cyan",
        >= 400 and < 500 => "yellow",
        _ => "red",
    };

    private static string ReasonPhrase(int statusCode)
    {
        var name = Enum.IsDefined(typeof(HttpStatusCode), statusCode)
            ? ((HttpStatusCode)statusCode).ToString()
            : null;

        return name is null ? string.Empty : Regex.Replace(name, "(?<!^)([A-Z])", " $1");
    }
}
