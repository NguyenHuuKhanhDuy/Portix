using System.ComponentModel;
using System.Net.Http;
using System.Net.Http.Json;
using Portix.Client.Api;
using Spectre.Console.Cli;

namespace Portix.Client.Cli.Commands;

public sealed class HttpCommand : AsyncCommand<HttpCommand.Settings>
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

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        Uri baseUri;
        if (settings.Api is not null)
        {
            baseUri = new Uri(settings.Api);
            Console.WriteLine("Checking for a running daemon...");
            await DaemonLauncher.EnsureRunningAsync(client, baseUri).ConfigureAwait(false);
        }
        else
        {
            Console.WriteLine("Starting an isolated daemon for this session...");
            baseUri = await DaemonLauncher.StartIsolatedAsync(client, cancellationToken).ConfigureAwait(false);
        }

        using var response = await client.PostAsJsonAsync(new Uri(baseUri, "/api/tunnels"), new { localPort = settings.Port, subdomain = settings.Subdomain }, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            Console.WriteLine($"Error: could not open tunnel: {error}");
            return 1;
        }

        var tunnel = await response.Content.ReadFromJsonAsync<TunnelDto>(cancellationToken: cancellationToken).ConfigureAwait(false);
        if (tunnel is null)
        {
            Console.WriteLine("Error: malformed response from daemon.");
            return 1;
        }

        Console.WriteLine($"Tunnel open: {tunnel.PublicUrl} -> localhost:{settings.Port}");
        Console.WriteLine($"Dashboard:   {baseUri}");
        Console.WriteLine("Press Ctrl+C to close this tunnel.");

        using var shutdownCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            shutdownCts.Cancel();
        };

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, shutdownCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // expected on Ctrl+C
        }

        Console.WriteLine("Closing tunnel...");
        using var deleteResponse = await client.DeleteAsync(new Uri(baseUri, $"/api/tunnels/{tunnel.Id}"), CancellationToken.None).ConfigureAwait(false);
        if (!deleteResponse.IsSuccessStatusCode && deleteResponse.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            Console.WriteLine($"Warning: failed to close tunnel cleanly ({deleteResponse.StatusCode}).");
        }

        return 0;
    }
}
