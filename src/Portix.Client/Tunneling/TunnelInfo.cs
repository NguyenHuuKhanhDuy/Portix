namespace Portix.Client.Tunneling;

public enum TunnelStatus
{
    Connecting,
    Online,
    Error,
    Closed,
}

/// <summary>One tunnel the daemon currently knows about, live-updated across reconnects.</summary>
public sealed class TunnelInfo
{
    public required string Id { get; set; }
    public required int LocalPort { get; init; }

    /// <summary>Subdomain the user asked for, if any — re-requested on reconnect; null means "assign randomly."</summary>
    public string? DesiredSubdomain { get; init; }

    public string? Subdomain { get; set; }
    public string? PublicUrl { get; set; }
    public TunnelStatus Status { get; set; } = TunnelStatus.Connecting;
    public string? LastError { get; set; }

    /// <summary>When false, the inspector still records method/path/status/duration but skips body previews.</summary>
    public bool CaptureBodiesEnabled { get; set; } = true;
}
