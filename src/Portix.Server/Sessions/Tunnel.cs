using System.Collections.Concurrent;

namespace Portix.Server.Sessions;

/// <summary>One registered tunnel: a subdomain routed to a specific session's control channel.</summary>
public sealed class Tunnel
{
    public required string TunnelId { get; init; }
    public required string Subdomain { get; init; }

    /// <summary>Informational only — the server never dials this port itself; the client resolves it.</summary>
    public required int LocalPortHint { get; init; }

    public required Session Session { get; init; }

    /// <summary>StreamIds this tunnel has an outstanding NEW_STREAM signal for, so teardown can fault them.</summary>
    public ConcurrentDictionary<string, byte> PendingStreamIds { get; } = new();
}
