namespace Portix.Client.Inspector;

/// <summary>
/// Wraps a stream and mirrors up to a capped number of bytes read through it into an
/// in-memory preview, without altering what the real reader sees or ever buffering more
/// than the cap — used to capture a request/response body preview while it streams through
/// forwarding, not after the fact.
/// </summary>
public sealed class CappedTeeStream : Stream
{
    private readonly Stream _inner;
    private readonly byte[] _preview;
    private int _previewLength;

    public CappedTeeStream(Stream inner, int capBytes)
    {
        _inner = inner;
        _preview = new byte[capBytes];
    }

    public bool Truncated { get; private set; }

    public ReadOnlySpan<byte> Preview => _preview.AsSpan(0, _previewLength);

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var read = await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        Mirror(buffer.Span[..read]);
        return read;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var read = await _inner.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
        Mirror(buffer.AsSpan(offset, read));
        return read;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = _inner.Read(buffer, offset, count);
        Mirror(buffer.AsSpan(offset, read));
        return read;
    }

    private void Mirror(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty || _previewLength >= _preview.Length)
        {
            if (!data.IsEmpty && _previewLength >= _preview.Length)
            {
                Truncated = true;
            }

            return;
        }

        var toCopy = Math.Min(data.Length, _preview.Length - _previewLength);
        data[..toCopy].CopyTo(_preview.AsSpan(_previewLength));
        _previewLength += toCopy;

        if (toCopy < data.Length)
        {
            Truncated = true;
        }
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() => _inner.Flush();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
