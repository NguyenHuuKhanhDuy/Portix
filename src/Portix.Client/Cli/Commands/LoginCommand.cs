using System.ComponentModel;
using Microsoft.Extensions.Configuration;
using Portix.Client.Cli;
using Portix.Client.Tunneling;
using Spectre.Console.Cli;

namespace Portix.Client.Cli.Commands;

public sealed class LoginCommand : AsyncCommand<LoginCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<token>")]
        [Description("Personal API token issued by the server's admin API")]
        public string Token { get; init; } = "";

        [CommandOption("--server")]
        [Description("Tunnel server base URL (defaults to the currently effective server URL if omitted)")]
        public string? Server { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var serverUrl = settings.Server ?? ResolveExistingServerUrl() ?? "http://localhost:5100";
        Console.WriteLine($"Using server {serverUrl}");

        var serverBaseUri = new Uri(serverUrl);
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        await using var channel = new ControlChannelClient(httpClient, serverBaseUri, settings.Token);

        try
        {
            await channel.ConnectAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }

        ClientConfigStore.Save(settings.Token, serverUrl);
        Console.WriteLine("Login successful. Token saved.");
        Console.WriteLine("If the daemon is already running, it will pick up the new token on its next reconnect attempt.");
        return 0;
    }

    // Mirrors Program.cs's daemon configuration source order (appsettings.json, then the
    // persisted config store on top) so a bare `login <token>` never resets an already-effective
    // server URL back to the hardcoded fallback.
    private static string? ResolveExistingServerUrl()
    {
        var builder = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile(ClientConfigStore.ConfigPath, optional: true);

        return builder.Build()["Portix:ServerUrl"];
    }
}
