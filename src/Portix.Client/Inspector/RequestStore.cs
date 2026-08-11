using System.Collections.Concurrent;

namespace Portix.Client.Inspector;

/// <summary>
/// Holds captured requests across every tunnel with two independent bounds: a fixed
/// per-tunnel count (oldest overwritten first) and a global cross-tunnel byte cap (globally
/// oldest evicted first). The <c>_captures</c> dictionary is the single source of truth —
/// the per-tunnel and global order queues may hold sequence numbers for captures already
/// removed by the other bound; lookups simply skip anything no longer present, so eviction
/// never needs an O(n) removal from either queue.
/// </summary>
public sealed class RequestStore
{
    private readonly int _maxPerTunnel;
    private readonly long _globalCapBytes;

    private readonly ConcurrentDictionary<long, CapturedRequest> _captures = new();
    private readonly ConcurrentDictionary<string, ConcurrentQueue<long>> _perTunnelOrder = new();
    private readonly ConcurrentQueue<long> _globalOrder = new();

    private long _nextSequence;
    private long _totalBytes;

    public RequestStore(int maxPerTunnel = 200, long globalCapBytes = 50 * 1024 * 1024)
    {
        _maxPerTunnel = maxPerTunnel;
        _globalCapBytes = globalCapBytes;
    }

    /// <summary>Raised whenever a new request is captured, for the SignalR hub to broadcast.</summary>
    public event Action<CapturedRequest>? Captured;

    public void Add(string tunnelId, Func<long, CapturedRequest> build)
    {
        var sequence = Interlocked.Increment(ref _nextSequence);
        var capture = build(sequence);
        _captures[sequence] = capture;

        var tunnelQueue = _perTunnelOrder.GetOrAdd(tunnelId, static _ => new ConcurrentQueue<long>());
        tunnelQueue.Enqueue(sequence);
        _globalOrder.Enqueue(sequence);
        Interlocked.Add(ref _totalBytes, capture.ApproxSizeBytes);

        EnforcePerTunnelCapacity(tunnelQueue);
        EnforceGlobalCap();

        Captured?.Invoke(capture);
    }

    public IReadOnlyList<CapturedRequest> List(string tunnelId, int limit = 100)
    {
        if (!_perTunnelOrder.TryGetValue(tunnelId, out var queue))
        {
            return Array.Empty<CapturedRequest>();
        }

        var result = new List<CapturedRequest>();
        foreach (var sequence in queue.ToArray().Reverse()) // most recent first
        {
            if (_captures.TryGetValue(sequence, out var capture))
            {
                result.Add(capture);
                if (result.Count >= limit)
                {
                    break;
                }
            }
        }

        return result;
    }

    public bool TryGet(string tunnelId, string requestId, out CapturedRequest capture)
    {
        capture = null!;
        if (!_perTunnelOrder.TryGetValue(tunnelId, out var queue))
        {
            return false;
        }

        foreach (var sequence in queue)
        {
            if (_captures.TryGetValue(sequence, out var found) && found.Id == requestId)
            {
                capture = found;
                return true;
            }
        }

        return false;
    }

    /// <summary>Empties one tunnel's captures; other tunnels are unaffected.</summary>
    public void Clear(string tunnelId)
    {
        if (!_perTunnelOrder.TryRemove(tunnelId, out var queue))
        {
            return;
        }

        foreach (var sequence in queue)
        {
            if (_captures.TryRemove(sequence, out var removed))
            {
                Interlocked.Add(ref _totalBytes, -removed.ApproxSizeBytes);
            }
        }
    }

    private void EnforcePerTunnelCapacity(ConcurrentQueue<long> tunnelQueue)
    {
        while (tunnelQueue.Count > _maxPerTunnel && tunnelQueue.TryDequeue(out var oldest))
        {
            if (_captures.TryRemove(oldest, out var removed))
            {
                Interlocked.Add(ref _totalBytes, -removed.ApproxSizeBytes);
            }
        }
    }

    private void EnforceGlobalCap()
    {
        while (Interlocked.Read(ref _totalBytes) > _globalCapBytes && _globalOrder.TryDequeue(out var oldest))
        {
            if (_captures.TryRemove(oldest, out var removed))
            {
                Interlocked.Add(ref _totalBytes, -removed.ApproxSizeBytes);
            }
        }
    }
}
