namespace Portix.Shared.Protocol;

/// <summary>
/// Pumps raw bytes concurrently in both directions of an already-accepted protocol upgrade
/// (e.g. a WebSocket, once the local app answered 101) until either direction reaches
/// end-of-stream or <paramref name="ct"/> is cancelled, then stops the other direction too.
/// Deliberately has no idle-read timeout: an upgraded connection can legitimately sit silent
/// far longer than an ordinary request/response body ever would, and that silence must not be
/// treated as a failure. Not used for ordinary request/response bodies — see
/// PublicForwardingMiddleware/RequestForwarder's own idle-timeout copy helpers for those.
/// </summary>
public static class DuplexPump
{
    /// <summary>
    /// Pumps <paramref name="sideAIn"/> into <paramref name="sideBOut"/> and
    /// <paramref name="sideBIn"/> into <paramref name="sideAOut"/> concurrently. The same stream
    /// may be passed as both the "in" and "out" of one side when that side is a single duplex
    /// stream (e.g. an upgraded HTTP/1.1 connection).
    /// </summary>
    public static async Task RunAsync(Stream sideAIn, Stream sideAOut, Stream sideBIn, Stream sideBOut, CancellationToken ct)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var aToB = CopyUntilClosedAsync(sideAIn, sideBOut, linkedCts.Token);
        var bToA = CopyUntilClosedAsync(sideBIn, sideAOut, linkedCts.Token);

        await Task.WhenAny(aToB, bToA).ConfigureAwait(false);
        linkedCts.Cancel();

        await AwaitIgnoringExpectedFailuresAsync(aToB).ConfigureAwait(false);
        await AwaitIgnoringExpectedFailuresAsync(bToA).ConfigureAwait(false);
    }

    private static async Task CopyUntilClosedAsync(Stream source, Stream destination, CancellationToken ct)
    {
        var buffer = new byte[16 * 1024];
        while (true)
        {
            var read = await source.ReadAsync(buffer, ct).ConfigureAwait(false);
            if (read == 0)
            {
                return;
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Once one direction ends, the other is cancelled and/or its underlying stream may already
    /// be torn down by the caller (e.g. the local app closed its socket) — both are expected,
    /// unremarkable ways for the *other* direction to end, not failures of the pump itself.
    /// </summary>
    private static async Task AwaitIgnoringExpectedFailuresAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException)
        {
        }
    }
}
