using System.Collections.Concurrent;
using Portix.Server.Data;
using Portix.Shared.Protocol;

namespace Portix.Server.Sessions;

/// <summary>One connected client: its control channel and every tunnel it currently has registered.</summary>
public sealed class Session
{
    public required string SessionId { get; init; }

    /// <summary>The authenticated user this session belongs to; tunnel quotas are enforced against their plan.</summary>
    public required User Owner { get; init; }

    /// <summary>Response body of the client's long-lived /control request; write ControlMessages here to reach the client.</summary>
    public required Stream ControlResponseStream { get; init; }

    /// <summary>Guards writes to <see cref="ControlResponseStream"/> since heartbeats, tunnel registration replies, and NEW_STREAM signals can all be written concurrently.</summary>
    public SemaphoreSlim ControlWriteLock { get; } = new(1, 1);

    public DateTimeOffset LastHeartbeatUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Cancelled when the session is torn down, so in-flight work can stop promptly.</summary>
    public CancellationTokenSource ShutdownTokenSource { get; } = new();

    /// <summary>Every tunnel currently registered under this session, keyed by TunnelId.</summary>
    public ConcurrentDictionary<string, Tunnel> Tunnels { get; } = new();

    /// <summary>Writes a control message to the client, serializing concurrent writers via <see cref="ControlWriteLock"/>.</summary>
    public async Task WriteControlMessageAsync(ControlMessage message, CancellationToken ct)
    {
        await ControlWriteLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await FrameCodec.WriteMessageAsync(ControlResponseStream, message, ct).ConfigureAwait(false);
        }
        finally
        {
            ControlWriteLock.Release();
        }
    }
}
