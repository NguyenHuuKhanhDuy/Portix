using System.Buffers.Binary;
using System.Text.Json;

namespace Portix.Shared.Protocol;

/// <summary>
/// Writes/reads the length-prefixed JSON <see cref="DataMessageHeader"/> that precedes
/// the raw body bytes on a /data/{streamId} leg.
/// </summary>
public static class DataMessageCodec
{
    public static async Task WriteHeaderAsync(Stream stream, DataMessageHeader header, CancellationToken ct = default)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(header);
        var lengthPrefix = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(lengthPrefix, payload.Length);
        await stream.WriteAsync(lengthPrefix, ct).ConfigureAwait(false);
        await stream.WriteAsync(payload, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    public static async Task<DataMessageHeader> ReadHeaderAsync(Stream stream, CancellationToken ct = default)
    {
        var lengthPrefix = new byte[4];
        await ReadExactAsync(stream, lengthPrefix, ct).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadInt32BigEndian(lengthPrefix);

        var payload = new byte[length];
        await ReadExactAsync(stream, payload, ct).ConfigureAwait(false);

        return JsonSerializer.Deserialize<DataMessageHeader>(payload)
            ?? throw new InvalidDataException("Empty data-message header.");
    }

    private static async Task ReadExactAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), ct).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("Stream ended mid-header.");
            }

            offset += read;
        }
    }
}
