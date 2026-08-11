using System.Collections.Concurrent;

namespace Portix.Server.Forwarding;

/// <summary>
/// Hands off a <see cref="DataChannel"/> from the /data/{streamId} endpoint (once the
/// client opens it) to the public-request handler that is waiting for it, keyed by streamId.
/// </summary>
public sealed class StreamBroker
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<DataChannel>> _pending = new();

    public Task<DataChannel> RegisterAndWait(string streamId, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<DataChannel>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(streamId, tcs))
        {
            throw new InvalidOperationException($"Stream id '{streamId}' is already registered.");
        }

        var registration = ct.Register(() =>
        {
            if (_pending.TryRemove(streamId, out var pendingTcs))
            {
                pendingTcs.TrySetCanceled(ct);
            }
        });

        return WaitAndDisposeRegistration(tcs.Task, registration);
    }

    private static async Task<DataChannel> WaitAndDisposeRegistration(Task<DataChannel> task, CancellationTokenRegistration registration)
    {
        using (registration)
        {
            return await task.ConfigureAwait(false);
        }
    }

    /// <summary>Called by the /data/{streamId} endpoint once the client opens its side. Returns false if no one is waiting (unknown or expired streamId).</summary>
    public bool Complete(string streamId, DataChannel channel)
    {
        if (_pending.TryRemove(streamId, out var tcs))
        {
            return tcs.TrySetResult(channel);
        }

        return false;
    }

    public void FaultAllPending(Exception exception, IEnumerable<string> streamIds)
    {
        foreach (var streamId in streamIds)
        {
            if (_pending.TryRemove(streamId, out var tcs))
            {
                tcs.TrySetException(exception);
            }
        }
    }
}
