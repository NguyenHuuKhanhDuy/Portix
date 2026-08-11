using Portix.Client.Cli;
using Spectre.Console.Cli;

namespace Portix.Client.Cli.Commands;

public sealed class LogoutCommand : AsyncCommand<LogoutCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
    }

    protected override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        ClientConfigStore.Clear();
        Console.WriteLine("Logged out. Saved token removed.");
        return Task.FromResult(0);
    }
}
