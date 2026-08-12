namespace Portix.Client.Cli.Commands;

public sealed class HttpsCommand : TunnelForwardCommandBase
{
    protected override string Scheme => "https";
}
