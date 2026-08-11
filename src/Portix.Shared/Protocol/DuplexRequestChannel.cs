using System.Net;
using System.Net.Http;

namespace Portix.Shared.Protocol;

/// <summary>
/// An <see cref="HttpContent"/> that hands its caller the real underlying network stream
/// once HttpClient starts sending the request, so writes reach the wire immediately instead
/// of sitting in an intermediate buffer waiting for more data or end-of-stream. This turns an
/// ordinary POST into a long-lived, one-directional stream the server reads incrementally.
/// Pair with <c>HttpCompletionOption.ResponseHeadersRead</c> so the response becomes readable
/// before this request body completes.
/// </summary>
public sealed class DuplexRequestChannel : HttpContent
{
    private readonly TaskCompletionSource<Stream> _streamReady =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _writingComplete =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Resolves once HttpClient begins sending this request body, to the real network stream.</summary>
    public Task<Stream> WriteStreamAsync => _streamReady.Task;

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        _streamReady.TrySetResult(stream);
        // The request body's END_STREAM is implied by this method returning, so we must not
        // return until the caller is done writing (CompleteWriting), even though nothing here
        // touches `stream` again in the meantime.
        await _writingComplete.Task.ConfigureAwait(false);
    }

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }

    public void CompleteWriting() => _writingComplete.TrySetResult();

    /// <summary>
    /// Waits for <see cref="WriteStreamAsync"/>, but races it against the SendAsync task it's
    /// paired with: if the connection attempt itself fails before HttpClient ever starts
    /// serializing this content, <see cref="WriteStreamAsync"/> never resolves on its own — it
    /// would hang forever waiting for a write stream that is never coming. Racing surfaces that
    /// failure immediately instead.
    /// </summary>
    public async Task<Stream> WaitForWriteStreamAsync(Task sendTask, CancellationToken ct)
    {
        var completed = await Task.WhenAny(WriteStreamAsync, sendTask).ConfigureAwait(false);
        if (completed == sendTask)
        {
            // Either faulted (surfaces the real connection error here instead of a hang) or
            // finished normally faster than expected; either way, await once to observe it.
            await sendTask.ConfigureAwait(false);
        }

        return await WriteStreamAsync.WaitAsync(ct).ConfigureAwait(false);
    }
}
