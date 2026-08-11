using System.ComponentModel;
using System.Net;
using System.Net.Http;
using Spectre.Console.Cli;

namespace Portix.Client.Cli.Commands;

public sealed class RmCommand : AsyncCommand<RmCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<id>")]
        [Description("Tunnel ID to close")]
        public string Id { get; init; } = "";

        [CommandOption("--api")]
        [Description("Daemon's local API base URL")]
        [DefaultValue("http://127.0.0.1:4040")]
        public string Api { get; init; } = "http://127.0.0.1:4040";
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var baseUri = new Uri(settings.Api);
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        await DaemonLauncher.EnsureRunningAsync(client, baseUri).ConfigureAwait(false);

        using var response = await client.DeleteAsync(new Uri(baseUri, $"/api/tunnels/{settings.Id}"), cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            Console.WriteLine($"No tunnel with id '{settings.Id}'.");
            return 1;
        }

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Error: failed to close tunnel ({response.StatusCode}).");
            return 1;
        }

        Console.WriteLine($"Closed tunnel '{settings.Id}'.");
        return 0;
    }
}
