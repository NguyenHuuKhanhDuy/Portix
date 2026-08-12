namespace Portix.Client.Cli.Commands;

public sealed class HttpCommand : TunnelForwardCommandBase
{
    protected override string Scheme => "http";
}
