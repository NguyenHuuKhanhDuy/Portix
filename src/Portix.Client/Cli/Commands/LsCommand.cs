using System.ComponentModel;
using System.Net.Http;
using System.Net.Http.Json;
using Portix.Client.Api;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Portix.Client.Cli.Commands;

public sealed class LsCommand : AsyncCommand<LsCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
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

        var tunnels = await client.GetFromJsonAsync<TunnelDto[]>(new Uri(baseUri, "/api/tunnels"), cancellationToken).ConfigureAwait(false)
            ?? Array.Empty<TunnelDto>();

        if (tunnels.Length == 0)
        {
            Console.WriteLine("No tunnels open.");
            return 0;
        }

        var table = new Table();
        table.AddColumn("ID");
        table.AddColumn("Port");
        table.AddColumn("URL");
        table.AddColumn("Status");

        foreach (var t in tunnels)
        {
            table.AddRow(t.Id, t.LocalPort.ToString(), t.PublicUrl ?? "-", t.Status);
        }

        AnsiConsole.Write(table);
        return 0;
    }
}
