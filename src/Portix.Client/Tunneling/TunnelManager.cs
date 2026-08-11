using System.Collections.Concurrent;

namespace Portix.Client.Tunneling;

/// <summary>
/// Single source of truth for every tunnel the daemon currently has open. The local REST API,
/// the SignalR hub, and the request forwarder all read from and write through this instance.
/// </summary>
public sealed class TunnelManager
{
    private static readonly TimeSpan ChannelWaitTimeout = TimeSpan.FromSeconds(10);

    private readonly ConcurrentDictionary<string, TunnelInfo> _tunnels = new();
    private readonly ILogger<TunnelManager> _logger;
    private ControlChannelClient? _channel;
    private TaskCompletionSource<bool> _channelReady = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Reason the most recent connection attempt failed, so a caller waiting on a channel that never
    /// existed yet (no tunnels to carry a per-tunnel error) can still learn why.</summary>
    public string? LastConnectionError { get; private set; }

    public TunnelManager(ILogger<TunnelManager> logger)
    {
        _logger = logger;
    }

    /// <summary>Raised whenever a tunnel is opened, closed, or changes status.</summary>
    public event Action<TunnelInfo>? TunnelChanged;

    /// <summary>Called by the control-channel background service once connected/reconnected, and cleared on disconnect.</summary>
    public void SetChannel(ControlChannelClient? channel)
    {
        _channel = channel;
        if (channel is not null)
        {
            _channelReady.TrySetResult(true);
        }
        else
        {
            _channelReady = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    public IReadOnlyCollection<TunnelInfo> List() => _tunnels.Values.ToArray();

    public bool TryGet(string tunnelId, out TunnelInfo tunnel) => _tunnels.TryGetValue(tunnelId, out tunnel!);

    /// <summary>
    /// Waits briefly for the control channel if it isn't connected yet — the daemon may have just
    /// self-started (see DaemonLauncher) and its local API can become reachable slightly before
    /// its handshake with the tunnel server completes.
    /// </summary>
    private async Task<ControlChannelClient> WaitForChannelAsync(CancellationToken ct)
    {
        if (_channel is { } readyChannel)
        {
            return readyChannel;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(ChannelWaitTimeout);
        try
        {
            await _channelReady.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            var message = LastConnectionError is { } reason
                ? $"Timed out waiting to connect to the tunnel server. Last error: {reason}"
                : "Timed out waiting to connect to the tunnel server.";
            throw new InvalidOperationException(message);
        }

        return _channel ?? throw new InvalidOperationException("Not connected to the tunnel server yet.");
    }

    public async Task<TunnelInfo> OpenAsync(int localPort, string? desiredSubdomain, CancellationToken ct)
    {
        var channel = await WaitForChannelAsync(ct).ConfigureAwait(false);

        var result = await channel.RegisterTunnelAsync(localPort, desiredSubdomain, ct).ConfigureAwait(false);

        var tunnel = new TunnelInfo
        {
            Id = result.TunnelId,
            LocalPort = localPort,
            DesiredSubdomain = desiredSubdomain,
            Subdomain = result.Subdomain,
            PublicUrl = result.PublicUrl,
            Status = TunnelStatus.Online,
        };

        _tunnels[tunnel.Id] = tunnel;
        TunnelChanged?.Invoke(tunnel);
        return tunnel;
    }

    public async Task CloseAsync(string tunnelId, CancellationToken ct)
    {
        if (!_tunnels.TryRemove(tunnelId, out var tunnel))
        {
            throw new KeyNotFoundException($"No tunnel with id '{tunnelId}'.");
        }

        tunnel.Status = TunnelStatus.Closed;
        TunnelChanged?.Invoke(tunnel);

        if (_channel is not null)
        {
            await _channel.UnregisterTunnelAsync(tunnelId, ct).ConfigureAwait(false);
        }
    }

    /// <summary>Re-registers every currently-known tunnel on a fresh connection after a reconnect.</summary>
    public async Task ReregisterAllAsync(CancellationToken ct)
    {
        var channel = _channel ?? throw new InvalidOperationException("Not connected to the tunnel server yet.");

        foreach (var tunnel in _tunnels.Values.ToArray())
        {
            try
            {
                var result = await channel.RegisterTunnelAsync(tunnel.LocalPort, tunnel.DesiredSubdomain, ct).ConfigureAwait(false);

                _tunnels.TryRemove(tunnel.Id, out _);
                tunnel.Id = result.TunnelId;
                tunnel.Subdomain = result.Subdomain;
                tunnel.PublicUrl = result.PublicUrl;
                tunnel.Status = TunnelStatus.Online;
                tunnel.LastError = null;
                _tunnels[tunnel.Id] = tunnel;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to re-register tunnel for port {Port} after reconnect.", tunnel.LocalPort);
                tunnel.Status = TunnelStatus.Error;
                tunnel.LastError = ex.Message;
            }

            TunnelChanged?.Invoke(tunnel);
        }
    }

    /// <summary>Marks every tunnel as errored when the control channel drops, without removing them — reconnect will retry.</summary>
    public void MarkAllDisconnected(string reason)
    {
        LastConnectionError = reason;

        foreach (var tunnel in _tunnels.Values)
        {
            tunnel.Status = TunnelStatus.Error;
            tunnel.LastError = reason;
            TunnelChanged?.Invoke(tunnel);
        }
    }
}
