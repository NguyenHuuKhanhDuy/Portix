using System.Buffers.Binary;
using System.Text.Json;

namespace Portix.Shared.Protocol;

/// <summary>
/// Reads/writes length-prefixed JSON frames: a 4-byte big-endian length
/// followed by that many bytes of UTF-8 JSON.
/// </summary>
public static class FrameCodec
{
    private const int MaxFrameLength = 1024 * 1024; // 1 MiB guards against a corrupt/hostile length prefix

    public static async Task WriteMessageAsync(Stream stream, ControlMessage message, CancellationToken ct = default)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(message);
        var lengthPrefix = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(lengthPrefix, payload.Length);
        await stream.WriteAsync(lengthPrefix, ct).ConfigureAwait(false);
        await stream.WriteAsync(payload, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Returns null when the stream ends cleanly before a new frame starts.</summary>
    public static async Task<ControlMessage?> ReadMessageAsync(Stream stream, CancellationToken ct = default)
    {
        var lengthPrefix = new byte[4];
        if (!await ReadExactAsync(stream, lengthPrefix, ct).ConfigureAwait(false))
        {
            return null;
        }

        var length = BinaryPrimitives.ReadInt32BigEndian(lengthPrefix);
        if (length < 0 || length > MaxFrameLength)
        {
            throw new InvalidDataException($"Control frame length {length} out of bounds.");
        }

        var payload = new byte[length];
        if (!await ReadExactAsync(stream, payload, ct).ConfigureAwait(false))
        {
            throw new EndOfStreamException("Stream ended mid-frame.");
        }

        return JsonSerializer.Deserialize<ControlMessage>(payload);
    }

    /// <summary>Fills <paramref name="buffer"/> completely, or returns false if the stream ended before any bytes were read.</summary>
    private static async Task<bool> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), ct).ConfigureAwait(false);
            if (read == 0)
            {
                return offset == 0 ? false : throw new EndOfStreamException("Stream ended mid-frame.");
            }

            offset += read;
        }

        return true;
    }
}
